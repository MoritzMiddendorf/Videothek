#tool "nuget:https://api.nuget.org/v3/index.json?package=GitVersion.CommandLine&version=3.6.2"
#addin "Cake.Incubator"
#addin "Cake.Json"
#load "./build/Settings.cake"

BuildSettings Settings;
Setup(ctx =>
{
    Settings = new BuildSettings(
       GitVersion(),
       DirectoryPath.FromString("./out")
    );
    Information(SerializeJsonPretty(BuildSystem));
});

Task("Restore-Dependencies")
.Does(() => {
    NuGetRestore("./src/Videothek.sln");
});

Task("Compile")
.IsDependentOn("Restore-Dependencies")
.Does(() => {
   MSBuild(
       "./src/Videothek.sln",
       new MSBuildSettings(){
           BinaryLogger = new MSBuildBinaryLogSettings() {
               Enabled = true,
               FileName = Settings.Directories.LogOutputDirectory + "/MSBuild.binlog"
           },
           ToolVersion = MSBuildToolVersion.VS2017,
           Configuration = Settings.Configuration,
           PlatformTarget = Settings.PlatformTarget,
           Verbosity = Verbosity.Minimal
       }
    );
});

Task("Publish")
.IsDependentOn("Compile")
.Does(() => {
    var ProjectsToBePublished = new CustomProjectParserResult[]{
        ParseProject("./src/Videothek.Terminal/Videothek.Terminal.csproj","Release")
    };
    foreach(var project in ProjectsToBePublished){
        CopyDirectory(project.OutputPath,Settings.Directories.BinaryOutputDirectory.Combine(project.AssemblyName));
    }
});


Task("Package") 
.IsDependentOn("Publish") 
.Does(() => { 
    var artifactFolders = GetDirectories(Settings.Directories.BinaryOutputDirectory.FullPath + "/**/*"); 
    foreach(var artifactFolder in artifactFolders){ 
        Zip(artifactFolder.FullPath,Settings.Directories.BinaryOutputDirectory.CombineWithFilePath(artifactFolder.GetDirectoryName() + ".zip")); 
    } 
}); 
 
Task("AppVeyor") 
.WithCriteria(() => AppVeyor.IsRunningOnAppVeyor) 
.IsDependentOn("Package") 
.Does(() => 
{ 
    AppVeyor.UpdateBuildVersion(Settings.Version.SemVer);
    foreach(var artifactZip in GetFiles(Settings.Directories.BinaryOutputDirectory + "/*.zip")){ 
        Information($"Uploading { artifactZip } to AppVeyor as '{ artifactZip.GetFilename()}'."); 
        BuildSystem.AppVeyor.UploadArtifact(artifactZip);
    } 
});

RunTarget("Package");