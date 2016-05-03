require 'albacore'
require 'fileutils'
require 'semver'

CLR_TOOLS_VERSION = 'v4.0.30319'
ARTIFACTS_PATH = File.expand_path("./build")
PROJECT_NAME = "WcfClientProxyGenerator"

$config = ENV['config'] || 'Debug'
$nuget_api_key = ENV['nuget_api_key']

task :default => :compile

desc 'Generate the VersionInfo.cs class'
asmver :version => [:versioning] do |a|
  git_data = commit_data()
  revision_hash = git_data[0]
  revision_date = git_data[1]
  
  a.file_path = "source/VersionInfo.cs"
  a.attributes assembly_version: FORMAL_VERSION,
    assembly_file_version: FORMAL_VERSION,
    assembly_product: PROJECT_NAME,
    assembly_description: "Git comit hash: #{revision_hash} - #{revision_date}",
    assembly_informational_version: BUILD_VERSION
end

desc 'Compile the project'
task :compile => ['nuget:restore'] do
  Rake::Task['build:net45'].invoke()
end

namespace :build do
  task :all => [:net45] do
  end

  build :net45 => ["nuget:restore", "version"] do |b|
    b.prop 'configuration', $config
    b.prop 'framework', 'NET45'
    b.target = ['clean', 'rebuild']
    b.file = "source/#{PROJECT_NAME}.sln"
  end    
end

desc 'Run tests'
test_runner :test => :compile do |tests|
  include FileUtils
  mkpath ARTIFACTS_PATH unless Dir.exists? ARTIFACTS_PATH

  tests.files = FileList["source/#{PROJECT_NAME}.Tests/bin/#{$config}/#{PROJECT_NAME}.Tests.dll"]
  tests.exe = nunit_path
  tests.add_parameter "/framework=#{CLR_TOOLS_VERSION}"
  tests.add_parameter "/noshadow"
  tests.add_parameter "/nologo"
  tests.add_parameter "/labels"
  tests.add_parameter "/xml=#{File.join(ARTIFACTS_PATH, "nunit-test-results.xml")}"
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
  desc 'Restores nuget packages'
  nugets_restore :restore do |p|
    p.out = "source/packages"
    p.exe = nuget_path
  end
  
  desc 'Creates the nuspec file'
  nugets_pack :pack => ['build:all'] do |p|
    p.configuration = 'Release'
    p.target = 'net45'
    p.files = FileList['source/WcfClientProxyGenerator/WcfClientProxyGenerator.csproj']
    p.exe = nuget_path    
    p.out = 'build'
    
    p.with_metadata do |m|
      m.id = PROJECT_NAME
      m.version = ENV['NUGET_VERSION']
      m.authors = "j.weber"
      m.description = "Utility to generate fault tolerant and highly configurable client proxies for WCF services based on WCF ServiceContracts. Supports making async calls using non async-ready ServiceContracts."
      m.project_url = "https://github.com/jweber/WcfClientProxyGenerator"
      m.title = PROJECT_NAME
      m.tags = "wcf service client proxy dynamic async"      
    end
  end

  task :push => [:pack] do
    raise "No NuGet API key was defined" unless $nuget_api_key
    
    nuget_package = "build\\#{PROJECT_NAME}.#{ENV['NUGET_VERSION']}.nupkg"
	  sh "#{nuget_path} push #{nuget_package} #{$nuget_api_key} -NonInteractive -Source https://www.nuget.org/api/v2/package"
  end
end

task :versioning do
  ver = SemVer.find
  #revision = (ENV['BUILD_NUMBER'] || ver.patch).to_i
  #ver = SemVer.new(ver.major, ver.minor, revision, ver.special)
  
  if ver.special != ''
    ENV['BUILD_VERSION'] = BUILD_VERSION = ver.format("%M.%m.%p.%s") + ".#{commit_data()[0]}"
    ENV['NUGET_VERSION'] = NUGET_VERSION = ver.format("%M.%m.%p-%s")
  else
    ENV['BUILD_VERSION'] = BUILD_VERSION = ver.format("%M.%m.%p") + ".#{commit_data()[0]}"
    ENV['NUGET_VERSION'] = NUGET_VERSION = ver.format("%M.%m.%p")  
  end
  
  ENV['FORMAL_VERSION'] = FORMAL_VERSION = "#{ ver.format("%M.%m.%p")}"
  puts "##teamcity[buildNumber '#{BUILD_VERSION}']"  
end

def nunit_path()
  File.join(Dir.glob(File.join('source', 'packages', "nunit.runners.*")).sort.last, "tools", "nunit-console.exe")
end

def nuget_path()
  File.join('lib', 'NuGet.exe')
end

def zip_directory(assemble_path, output_path)
  require 'albacore/tools/zippy'
  
  zf = Zippy.new assemble_path, output_path
  zf.write

#  zip = ZipDirectory.new
#  zip.directories_to_zip assemble_path
#  zip.output_path = File.dirname(output_path)
#  zip.output_file = File.basename(output_path)
#  zip.execute  
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
 
