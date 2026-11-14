$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$source=Join-Path $projectRoot 'src\DockForWindows\Shaders\LiquidGlass.hlsl'
$destination=Join-Path $projectRoot 'src\DockForWindows\Shaders\LiquidGlass.ps'
$lightingDestination=Join-Path $projectRoot 'src\DockForWindows\Shaders\LiquidGlass.ControlCenterLighting.ps'
$fusedSource=Join-Path $projectRoot 'src\DockForWindows\Shaders\LiquidGlass.Fused.hlsl'
$fusedDestination=Join-Path $projectRoot 'src\DockForWindows\Shaders\LiquidGlass.Fused.ps'
function NeedsCompile($target,$from=$source){!(Test-Path -LiteralPath $target)-or(Get-Item -LiteralPath $target).LastWriteTimeUtc-lt(Get-Item -LiteralPath $from).LastWriteTimeUtc}
if(!(NeedsCompile $destination)-and!(NeedsCompile $lightingDestination)-and!(NeedsCompile $fusedDestination $fusedSource)){return}
if(!('DockShaderCompiler' -as [type])){
Add-Type -TypeDefinition @'
using System; using System.IO; using System.Runtime.InteropServices;
public static class DockShaderCompiler {
 [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
 private interface Blob { [PreserveSig] IntPtr GetBufferPointer(); [PreserveSig] UIntPtr GetBufferSize(); }
 [DllImport("d3dcompiler_47.dll",CharSet=CharSet.Ansi)] private static extern int D3DCompile(byte[] data,UIntPtr size,string source,IntPtr defines,IntPtr include,string entry,string target,uint flags,uint flags2,out Blob code,out Blob errors);
 public static void Compile(string source,string destination,bool controlCenter=false){
  var bytes=controlCenter?System.Text.Encoding.UTF8.GetBytes("#define CONTROL_CENTER_LIGHTING\n"+File.ReadAllText(source)):File.ReadAllBytes(source);Blob code=null,errors=null;
  try{int result=D3DCompile(bytes,(UIntPtr)bytes.Length,source,IntPtr.Zero,IntPtr.Zero,"main","ps_3_0",1u<<15,0,out code,out errors);
   if(result<0)throw new Exception(errors==null?"Shader compilation failed":Marshal.PtrToStringAnsi(errors.GetBufferPointer()));
   var compiled=new byte[(int)code.GetBufferSize().ToUInt64()];Marshal.Copy(code.GetBufferPointer(),compiled,0,compiled.Length);File.WriteAllBytes(destination,compiled);
  }finally{if(code!=null)Marshal.FinalReleaseComObject(code);if(errors!=null)Marshal.FinalReleaseComObject(errors);}
 }
}
'@
}
if(NeedsCompile $destination){[DockShaderCompiler]::Compile($source,$destination,$false)}
if(NeedsCompile $lightingDestination){[DockShaderCompiler]::Compile($source,$lightingDestination,$true)}
if(NeedsCompile $fusedDestination $fusedSource){[DockShaderCompiler]::Compile($fusedSource,$fusedDestination,$false)}
