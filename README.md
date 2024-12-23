# Matebook E Go Goodies

Alternative to Huawei PC Manager

## Usage

```
Usage: kbd-detach [-q]
  Queries current status
  -q: Use exit status instead of stdout

Usage: kbd-detach [enable|disable]
  Enables or disables detached keyboard support
```

```
Description:
  QDCM loader for Gaokun

Usage:
  qdcm-loader [options]

Options:
  --preset <DisplayP3|sRGB>  Load factory calibration
  --reset                    Reset display color
  --igc <file.cube>          Load custom input shaper (3x1D LUT applied before 3D LUT)
  --3dlut <file.cube>        Load custom 3D LUT
  -?, -h, --help             Show help and usage information

Remarks:
  Both IRIDAS and Resolve .cube format are supported.
```

```console
PS C:\path\to\goodies>.\Set-ChargeLimit.ps1 -PercentageLimit 80
```

## Notes

Both kbd-detach and qdcm-loader depend on amd64 DLLs and are built targeting amd64/arm64ec.


No GUI yet because no modern GUI framework supports arm64ec.
