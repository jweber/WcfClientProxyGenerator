@echo off

echo Setting up rake environment for building

echo Installing Rake
call gem install rake

echo Installing Albacore
call gem install albacore

echo Installing Semver
call gem install semver