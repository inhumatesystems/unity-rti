#!/bin/bash -e

GITLAB_USERNAME="${GITLAB_USERNAME:=foobar}" # shouldn't be needed

# This script creates a new .NET project, adds Inhumate.RTI package, and
# copies relevant DLLs into the Unity project.

cd "$(dirname $0)/.."

dlls="\
    Inhumate.RTI.dll \
    Google.Protobuf.dll \
    Utf8Json.dll \
    System.Threading.Tasks.Extensions.dll \
    System.Memory.dll \
    System.Runtime.CompilerServices.Unsafe.dll \
    System.Buffers.dll \
    "

clientdir="tmp-update-dependencies"
builddir="$clientdir/bin/Release/netstandard2.0/publish"

if ! which dotnet >/dev/null; then
    echo "Please install .NET SDK"
    exit 1
fi

rm -rf $clientdir
mkdir -p $clientdir
cd $clientdir
dotnet new classlib -f netstandard2.0
dotnet add package Inhumate.RTI
dotnet publish -c Release
cd -

mkdir -p "./Packages/com.inhumatesystems.rti/Plugins"
for dll in $dlls; do
    cp -f "$builddir/$dll" "./Packages/com.inhumatesystems.rti/Plugins/"
done

grep Version $clientdir/*.csproj

rm -rf $clientdir
