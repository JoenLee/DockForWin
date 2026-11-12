param([string]$OutputFile)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
function Rounded-Path([single]$x,[single]$y,[single]$w,[single]$h,[single]$r){
 $p=[System.Drawing.Drawing2D.GraphicsPath]::new();$d=$r*2
 $p.AddArc($x,$y,$d,$d,180,90);$p.AddArc($x+$w-$d,$y,$d,$d,270,90);$p.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90);$p.AddArc($x,$y+$h-$d,$d,$d,90,90);$p.CloseFigure();return ,$p
}
$images=[System.Collections.Generic.List[byte[]]]::new();$sizes=@(16,24,32,48,64,128,256)
foreach($size in $sizes){
 $bitmap=[System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $g=[System.Drawing.Graphics]::FromImage($bitmap);$g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias;$g.ScaleTransform($size/64.0,$size/64.0)
 $blue=[System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.RectangleF]::new(0,0,64,64),[System.Drawing.Color]::FromArgb(0,153,255),[System.Drawing.Color]::FromArgb(45,76,226),[single]70)
 $shape=Rounded-Path 1 1 62 62 16;$g.FillPath($blue,$shape);$shape.Dispose();$blue.Dispose()
 $glass=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(95,255,255,255));$shape=Rounded-Path 7 35 50 18 7;$g.FillPath($glass,$shape);$shape.Dispose();$glass.Dispose()
 foreach($rect in @(@(13,25,10,19),@(27,15,11,29),@(42,25,10,19))){$shape=Rounded-Path $rect[0] $rect[1] $rect[2] $rect[3] 3;$g.FillPath([System.Drawing.Brushes]::White,$shape);$shape.Dispose()}
 $stream=[System.IO.MemoryStream]::new();$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png);$images.Add($stream.ToArray());$stream.Dispose();$g.Dispose();$bitmap.Dispose()
}
New-Item -ItemType Directory -Path(Split-Path $OutputFile -Parent) -Force|Out-Null
$file=[System.IO.File]::Create($OutputFile);$writer=[System.IO.BinaryWriter]::new($file)
try{$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
 for($i=0;$i-lt$sizes.Count;$i++){$dimension=if($sizes[$i]-eq256){0}else{$sizes[$i]};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$images[$i].Length);$writer.Write([uint32]$offset);$offset+=$images[$i].Length}
 foreach($bytes in $images){$writer.Write($bytes)}
}finally{$writer.Dispose();$file.Dispose()}
