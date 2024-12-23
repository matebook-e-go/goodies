#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <stdint.h>
#include <stdio.h>
#include <stdbool.h>
#include <stdatomic.h>

#include "getopt.h"

const wchar_t *w32strerror(DWORD err) {
    __declspec(thread) static wchar_t buf[4096];
    FormatMessageW(FORMAT_MESSAGE_FROM_SYSTEM, NULL, err, 0, buf, sizeof(buf), NULL);
    return buf;
}

static void print_help(const char *argv0) {
    printf("Usage: %s [-q]\n", argv0);
    printf("  Queries current status\n");
    printf("  -q: Use exit status instead of stdout\n\n");
    printf("Usage: %s [enable|disable]\n", argv0);
    printf("  Enables or disables detached keyboard support\n\n");
}

typedef int (*CallbackFunc)(int);

static atomic_int callback_arg;
static _Atomic(HANDLE) callback_event;

static int callback(int arg) {
    callback_arg = arg;
    atomic_store_explicit(&callback_arg, arg, memory_order_release);
    if (callback_event != NULL) {
        SetEvent(callback_event);
    }
    return 0;
}

// waits the thread to enter a blocking state
static int wait_thread_until_block(HANDLE thread) {
    uint64_t lastcycle = 0, cycle = 0;
    int count = 0;
    while (1) {
        if (!QueryThreadCycleTime(thread, &cycle)) return 0;
        uint64_t delta = cycle - lastcycle;
        lastcycle = cycle;
        if (delta == 0) {
            count++;
            if (count > 200) {
                return 1;
            }
        } else {
            count = 0;
        }
        SwitchToThread();
    }
}


static uint8_t pattern_at_GetInterruptPipeMsg_0[] = {
	0x40, 0x55, // push rbp
    0x48, 0x8D, 0xAC, 0x24, 0x30, 0xFF, 0xFF, 0xFF, // lea rbp, [rsp-0d0h]
    0x48, 0x81,	0xEC, 0xD0, 0x01, 0x00, 0x00, // sub rsp, 1d0h
    0x48, 0x8B, 0x05 // mov rax, qword ptr [rip+imm32]
};

static uint8_t pattern_at_GetInterruptPipeMsg_0x18[] = {
	0x48, 0x33, 0xC4, // xor rax, rsp
    0x48, 0x89, 0x45, 0x70, // mov qword ptr [rbp+70h], rax
    0xE8 // call imm32
};

static uint8_t pattern_at_GetInterruptPipeMsg_0x24[] = {
    0x48, 0x89, 0x05 // mov qword ptr [rip+imm32], rax
};

#define CHECK_PATTERN(addr, pattern) (memcmp((void*)(addr), (pattern), sizeof(pattern)) == 0)

int main(int argc, char** argv) {
    int query = 1;
    int quiet = 0;
    int set = -1;
    while (1) {
        int c = getopt(argc, argv, "hq");
        if (c == -1) break;
        switch (c) {
            case 'q':
                quiet = 1;
                break;
            default:
                print_help(argv[0]);
                return 1;
        }
    }
    if (optind < argc) {
        if (strcmp(argv[optind], "enable") == 0) {
            set = 1;
        } else if (strcmp(argv[optind], "disable") == 0) {
            set = 0;
        } else {
            printf("Unknown argument: %s\n", argv[optind]);
            print_help(argv[0]);
            return 1;
        }
    }

    HMODULE lib = LoadLibraryW(L"KeyboardService.dll");
    if (!lib) {
        fprintf(stderr, "Failed to load KeyboardService.dll: %ls\n", w32strerror(GetLastError()));
        return 255;
    }

    void (*ProcPipeMsg)(void) = (void*)GetProcAddress(lib, "ProcPipeMsg");
    void (*GetInterruptPipeMsg)(void) = (void*)GetProcAddress(lib, "GetInterruptPipeMsg");
    void (*StopProcPipeMsg)(void) = (void*)GetProcAddress(lib, "StopProcPipeMsg");
    void (*StopLoop)(void) = (void*)GetProcAddress(lib, "StopLoop");
    void (*CommandSendKbdDetachSupportGet)(void) = (void*)GetProcAddress(lib, "CommandSendKbdDetachSupportGet");
    void (*CommandSendKbdDetachSupportSet)(uint8_t) = (void*)GetProcAddress(lib, "CommandSendKbdDetachSupportSet");
    void (*RegisterCallBackKbdDetachSupportGet)(CallbackFunc) = (void*)GetProcAddress(lib, "RegisterCallBackKbdDetachSupportGet");

    volatile HANDLE* pg_DeviceHandle = NULL;

    if (CHECK_PATTERN(GetInterruptPipeMsg, pattern_at_GetInterruptPipeMsg_0) &&
        CHECK_PATTERN((uint8_t*)GetInterruptPipeMsg + 0x18, pattern_at_GetInterruptPipeMsg_0x18) &&
        CHECK_PATTERN((uint8_t*)GetInterruptPipeMsg + 0x24, pattern_at_GetInterruptPipeMsg_0x24)) {
        int32_t imm = *(int32_t*)((uint8_t*)GetInterruptPipeMsg + 0x24 + sizeof(pattern_at_GetInterruptPipeMsg_0x24));
        uintptr_t nextinst = (uintptr_t)GetInterruptPipeMsg + 0x24 + 7;
        pg_DeviceHandle = (HANDLE*)(nextinst + imm);
    }

    if (set == -1 && query) {
        // safe to cast LPTHREAD_START_ROUTINE here because lpParameter is passed by register
        HANDLE thr1 = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)ProcPipeMsg, NULL, 0, NULL);
        if (!thr1) {
            fprintf(stderr, "Failed to create ProcPipeMsg thread: %ls\n", w32strerror(GetLastError()));
            return 255;
        }
        HANDLE thr2 = CreateThread(NULL, 0, (LPTHREAD_START_ROUTINE)GetInterruptPipeMsg, NULL, 0, NULL);
        if (!thr2) {
            fprintf(stderr, "Failed to create GetInterruptPipeMsg thread: %ls\n", w32strerror(GetLastError()));
            return 255;
        }

        // no response before the device opened
        if (pg_DeviceHandle) {
            while ((atomic_thread_fence(memory_order_acquire), 
            *pg_DeviceHandle == INVALID_HANDLE_VALUE)) SwitchToThread();
        } else {
            // fallback to cycles polling
            wait_thread_until_block(thr1);
            wait_thread_until_block(thr2);
        }

        CloseHandle(thr1);
        CloseHandle(thr2);

        RegisterCallBackKbdDetachSupportGet(callback);

        HANDLE evt = CreateEventW(NULL, FALSE, FALSE, NULL);
        callback_event = evt;
        
        DWORD wait_result;
        for (int retry = 0; retry < 3; retry++) {
            CommandSendKbdDetachSupportGet();
            wait_result = WaitForSingleObject(callback_event, 100);
            if (wait_result == WAIT_TIMEOUT) {
                continue;
            }
            break;
        }
        
        CloseHandle(evt);

        if (wait_result == WAIT_TIMEOUT) {
            fprintf(stderr, "Timeout while waiting for response\n");
            return 255;
        } else if (wait_result != WAIT_OBJECT_0) {
            fprintf(stderr, "Failed to wait for response: %ls\n", w32strerror(GetLastError()));
            return 255;
        }
        if (quiet) {
            return !callback_arg;
        } else {
            puts(callback_arg ? "enabled" : "disabled");
        }
    } else {
        CommandSendKbdDetachSupportSet((uint8_t)set);
    }
    return 0;
}
