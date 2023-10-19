#!/bin/bash -e

# This script uses a local RTI repo

cd "$(dirname $0)/.."

dlls="\
    Inhumate.RTI.Client.dll \
    Google.Protobuf.dll \
    Utf8Json.dll \
    System.Threading.Tasks.Extensions.dll \
    System.Memory.dll \
    System.Runtime.CompilerServices.Unsafe.dll \
    System.Buffers.dll \
    "

clientdir="../../rti/clients/dotnet/src"
builddir="$clientdir/bin/Release/netstandard2.0/publish"

cd $clientdir
dotnet build
dotnet publish -c Release
cd -

mkdir -p "./Packages/com.inhumatesystems.rti/Plugins"
for dll in $dlls; do
    cp -f "$builddir/$dll" "./Packages/com.inhumatesystems.rti/Plugins/"
done
