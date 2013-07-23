require 'albacore'
require 'fileutils'
require 'semver'

CLR_TOOLS_VERSION = 'v4.0.30319'
ARTIFACTS_PATH = "build"
PROJECT_NAME = "WcfClientProxyGenerator"

$config = ENV['config'] || 'Debug'
$nuget_api_key = ENV['nuget_api_key']

task :default => :compile

desc 'Generate the VersionInfo.cs class'
assemblyinfo :version => [:versioning] do |asm|
  git_data = commit_data()
  revision_hash = git_data[0]
  revision_date = git_data[1]
    
  asm.version = FORMAL_VERSION
  asm.file_version = FORMAL_VERSION
  asm.product_name = PROJECT_NAME
  asm.description = "Git commit hash: #{revision_hash} - #{revision_date}"
  asm.custom_attributes :AssemblyInformationalVersion => "#{BUILD_VERSION}"
  asm.output_file = "source/VersionInfo.cs"
  asm.namespaces 'System', 'System.Reflection'  
end

desc 'Compile the project'
task :compile do
  Rake::Task['build:net45'].invoke()
end

namespace :build do
  task :all => [:net40, :net45] do
  end

  msbuild :net40 => :version do |msb|
    msb.properties :configuration => $config, :Framework => 'NET40'
    msb.targets [:clean, :build]
    msb.solution = "source/#{PROJECT_NAME}.sln"
  end
  
  msbuild :net45 => :version do |msb|
    msb.properties :configuration => $config, :Framework => 'NET45'
    msb.targets [:clean, :build]
    msb.solution = "source/#{PROJECT_NAME}.sln"
  end    
end

desc 'Run tests'
nunit :test => :compile do |nunit|
  include FileUtils
  mkpath ARTIFACTS_PATH unless Dir.exists? ARTIFACTS_PATH
  
  nunit.command = nunit_path
  nunit.assemblies "source/#{PROJECT_NAME}.Tests/bin/#{$config}/#{PROJECT_NAME}.Tests.dll"
  #nunit.options '/xml=nunit-console-output.xml'
  
  nunit.options = "/framework=#{CLR_TOOLS_VERSION}", '/noshadow', '/nologo', '/labels', "\"/xml=#{File.join(ARTIFACTS_PATH, "nunit-test-results.xml")}\""
end

desc 'Builds release package'
task :package => 'build:all' do
  include FileUtils
  
  assemble_path = File.join(ARTIFACTS_PATH, "assemble")
  
  mkpath ARTIFACTS_PATH unless Dir.exists? ARTIFACTS_PATH
  rm_rf Dir.glob(File.join(ARTIFACTS_PATH, "**/*.zip"))
  rm_rf assemble_path if Dir.exists? assemble_path
    
  mkpath assemble_path unless Dir.exists? assemble_path 
  rm_rf Dir.glob(File.join(assemble_path, "**/*"))
    
  cp_r Dir.glob("source/#{PROJECT_NAME}/bin/#{$config}/**"), assemble_path, :verbose => true
  rm Dir.glob("#{assemble_path}/log.*")
     
  zip_directory(assemble_path, File.join(ARTIFACTS_PATH, "#{PROJECT_NAME}-#{BUILD_VERSION}.zip"))
  rm_rf assemble_path if Dir.exists? assemble_path
end

namespace :nuget do
  desc 'Creates the nuspec file'
  nuspec :spec => :version do |nuspec|
    mkpath ARTIFACTS_PATH unless Dir.exists? ARTIFACTS_PATH
    
    nuspec.id = PROJECT_NAME
    nuspec.version = ENV['NUGET_VERSION']
    nuspec.authors = "j.weber"
    nuspec.description = "Utility to generate fault tolerant and retry capable dynamic proxies for WCF services based on the WCF service interface."
    nuspec.projectUrl = "https://github.com/jweber/WcfClientProxyGenerator"
    nuspec.title = PROJECT_NAME
    nuspec.tags = "wcf service proxy dynamic"
    nuspec.file "..\\source\\#{PROJECT_NAME}\\bin\\#{$config}\\net-4.0\\#{PROJECT_NAME}.dll", 'lib\net40'  
    nuspec.file "..\\source\\#{PROJECT_NAME}\\bin\\#{$config}\\net-4.5\\#{PROJECT_NAME}.dll", 'lib\net45'
    
    nuspec.working_directory = 'build'
    nuspec.output_file = "#{PROJECT_NAME}.nuspec"
  end
  
  nugetpack :pack => ['build:all', :spec] do |nuget|
    nuget.command = nuget_path
    nuget.nuspec = "build\\#{PROJECT_NAME}.nuspec"
    nuget.base_folder = 'build'
    nuget.output = 'build'
  end
  
  nugetpush :push => [:pack] do |nuget|
    raise "No NuGet API key was defined" unless $nuget_api_key
  
    nuget.command = nuget_path
    nuget.package = "build\\#{PROJECT_NAME}.#{ENV['NUGET_VERSION']}.nupkg"
    nuget.create_only = false
    nuget.apikey = $nuget_api_key
    nuget.create_only = false
  end
end

desc 'Builds version environment variables'
task :versioning do
  ver = SemVer.find
  revision = (ENV['BUILD_NUMBER'] || ver.patch).to_i
  var = SemVer.new(ver.major, ver.minor, revision, ver.special)
  
  ENV['BUILD_VERSION'] = BUILD_VERSION = ver.format("%M.%m.%p%s") + ".#{commit_data()[0]}"
  ENV['NUGET_VERSION'] = NUGET_VERSION = ver.format("%M.%m.%p%s")
  ENV['FORMAL_VERSION'] = FORMAL_VERSION = "#{ SemVer.new(ver.major, ver.minor, revision).format "%M.%m.%p"}"
  puts "##teamcity[buildNumber '#{BUILD_VERSION}']"  
end

def nunit_path()
  File.join(Dir.glob(File.join('lib', 'nuget_packages', "nunit.runners.*")).sort.last, "tools", "nunit-console.exe")
end

def nuget_path()
  File.join(Dir.glob(File.join('lib', 'nuget_packages', "nuget.commandline.*")).sort.last, "tools", "nuget.exe")
end

def zip_directory(assemble_path, output_path)
  zip = ZipDirectory.new
  zip.directories_to_zip assemble_path
  zip.output_path = File.dirname(output_path)
  zip.output_file = File.basename(output_path)
  zip.execute  
end

def commit_data
  begin
    commit = `git rev-parse --short HEAD`.chomp()
    git_date = `git log -1 --date=iso --pretty=format:%ad`
    commit_date = DateTime.parse(git_date).strftime("%Y-%m-%d %H%M%S")
  rescue Exception => e
    puts e.inspect
    commit = (ENV['BUILD_VCS_NUMBER'] || "000000")
    commit_date = Time.new.strftime("%Y-%m-%d %H%M%S")
  end
  [commit, commit_date]
 end
 