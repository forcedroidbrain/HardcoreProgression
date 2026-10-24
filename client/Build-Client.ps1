param([Parameter(Mandatory)][string]$GameRoot)
$ErrorActionPreference='Stop'
$output=Join-Path $PSScriptRoot 'output'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$null=[Reflection.Assembly]::LoadFrom("$PSHOME/Microsoft.CodeAnalysis.dll")
$null=[Reflection.Assembly]::LoadFrom("$PSHOME/Microsoft.CodeAnalysis.CSharp.dll")
$parse=[Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::Default.WithLanguageVersion([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::Latest)
$options=[Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]::DynamicallyLinkedLibrary).WithOptimizationLevel([Microsoft.CodeAnalysis.OptimizationLevel]::Release).WithNullableContextOptions([Microsoft.CodeAnalysis.NullableContextOptions]::Enable).WithDeterministic($true)
$references=[Collections.Generic.List[Microsoft.CodeAnalysis.MetadataReference]]::new()
$paths=@(Get-ChildItem "$GameRoot/EscapeFromTarkov_Data/Managed" -Filter '*.dll' | ForEach-Object FullName)+@("$GameRoot/BepInEx/core/BepInEx.dll","$GameRoot/BepInEx/core/0Harmony.dll","$GameRoot/BepInEx/plugins/spt/spt-reflection.dll","$GameRoot/BepInEx/plugins/spt/spt-common.dll")
foreach($path in $paths){$references.Add([Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($path))}
$trees=[Collections.Generic.List[Microsoft.CodeAnalysis.SyntaxTree]]::new()
$shared=Join-Path $PSScriptRoot '../src/Shared/QuestProgressDisplay.cs'
if(!(Test-Path -LiteralPath $shared)){$shared=Join-Path $PSScriptRoot 'Shared/QuestProgressDisplay.cs'}
foreach($path in @((Join-Path $PSScriptRoot 'QuestProgressClient.cs'),$shared)){$trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($path),$parse,$path))}
$attributes=@'
[assembly: System.Reflection.AssemblyVersion("2.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("2.0.0.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETStandard,Version=v2.1", FrameworkDisplayName = ".NET Standard 2.1")]
'@
$trees.Add([Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText($attributes,$parse))
$compilation=[Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create('HardcoreProgression.Client',$trees,$references,$options)
$target=Join-Path $output 'HardcoreProgression.Client.dll'
$stream=[IO.File]::Create($target)
try{$result=$compilation.Emit($stream)}finally{$stream.Dispose()}
$result.Diagnostics | Where-Object Severity -in 'Error','Warning' | ForEach-Object ToString | Select-Object -Unique
if(!$result.Success){throw 'Client build failed.'}
"PASS client build: $target"
