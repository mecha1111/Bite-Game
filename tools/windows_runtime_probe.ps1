param([string]$Command = 'get_tree', [string]$ParamsJson = '{}', [string]$Out, [switch]$Summary, [int]$ClickX=-1, [int]$ClickY=-1, [string]$Key)
if ($ClickX -ge 0 -or $Key) {
    foreach ($pressed in @($true,$false)) {
        if ($Key) { $inputCommand='inject_key'; $inputParams=@{key_label=$Key;pressed=$pressed} }
        else { $inputCommand='inject_mouse_click'; $inputParams=@{x=$ClickX;y=$ClickY;pressed=$pressed} }
        & $PSCommandPath -Command $inputCommand -ParamsJson ($inputParams|ConvertTo-Json -Compress)
        Start-Sleep -Milliseconds 60
    }
    return
}
$client = [System.Net.Sockets.TcpClient]::new('127.0.0.1', 7777)
$stream = $client.GetStream()
$reader = [System.IO.BinaryReader]::new($stream)
$welcome = [System.Text.Encoding]::UTF8.GetString($reader.ReadBytes($reader.ReadInt32()))
$request = @{id=1; command=$Command; params=($ParamsJson | ConvertFrom-Json)} | ConvertTo-Json -Depth 30 -Compress
$bytes = [System.Text.Encoding]::UTF8.GetBytes($request)
$stream.Write($bytes, 0, $bytes.Length)
$response = [System.Text.Encoding]::UTF8.GetString($reader.ReadBytes($reader.ReadInt32()))
$client.Close()
if ($Out) { [System.IO.File]::WriteAllText($Out, $response) }
elseif ($Summary) {
    function Show-Node($node) {
        $p = $node.properties
        '{0} [{1}] visible={2} text={3} disabled={4} pos={5} size={6} frames={7}' -f $node.path,$node.type,$p.visible,$p.text,$p.disabled,($p.position|ConvertTo-Json -Compress),($p.size|ConvertTo-Json -Compress),($p.sprite_frames|ConvertTo-Json -Compress)
        foreach ($child in $node.children) { Show-Node $child }
    }
    Show-Node (($response | ConvertFrom-Json).root)
} else { $response }
