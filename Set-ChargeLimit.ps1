[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [int]$PercentageLimit
)

$ErrorActionPreference = "stop"

$PercentageLimit = [Math]::Max($PercentageLimit, 50)
$PercentageLimit = [Math]::Min($PercentageLimit, 100)
$request = New-Object -TypeName byte[] -ArgumentList 64
$request[0] = 0x03 # MFID
$request[1] = 0x15 # SFID = SBCM
$request[2] = 0x01 # \SBCM.CHMD
$request[3] = 0x18 # \SBCM.DELY
$request[4] = $PercentageLimit-5  # \SBCM.STCP start charge percentage threshold
$request[5] = $PercentageLimit    # \SBCM.SOCP stop charge percentage threshold

$inst = Get-WmiObject -Namespace ROOT\WMI -Class OemWMIMethod
[void]$inst.OemWMIfun($request)
