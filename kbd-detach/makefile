
EXE = kbd-detach.exe
OBJECTS = kbd-detach.obj getopt.obj

CFLAGS = /std:c11 /experimental:c11atomics

PRIVATE_GENERIC_CFLAGS = /nologo

!IF "$(VSCMD_ARG_TGT_ARCH)" == "arm64"
PRIVATE_ARCH_CFLAGS = /arm64EC
!ELSE IF "$(VSCMD_ARG_TGT_ARCH)" != "x64"
!ERROR "Unsupported architecture: $(VSCMD_ARG_TGT_ARCH)"
!ENDIF

PRIVATE_CFLAGS = $(PRIVATE_GENERIC_CFLAGS) $(PRIVATE_ARCH_CFLAGS)

$(EXE): $(OBJECTS)
	cl $(CFLAGS) $(PRIVATE_CFLAGS) $(OBJECTS) /link $(LDFLAGS) /out:$@
	mt -nologo -manifest utf8.manifest -outputresource:$@;1

.c.obj:
	cl $(CFLAGS) $(PRIVATE_CFLAGS) /Fo:$@ /c $<

clean:
	del /q $(EXE) $(OBJECTS)
