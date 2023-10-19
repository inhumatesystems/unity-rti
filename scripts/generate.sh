#!/usr/bin/env sh
cd "$(dirname $0)"
rm -rf ../Packages/com.inhumatesystems.rti/Runtime/Generated/*
mkdir -p ../Packages/com.inhumatesystems.rti/Runtime/Generated
protoc=protoc
if [ -e "../protobuf/bin/protoc" ]; then
    protoc="../protobuf/bin/protoc"
elif [ -e "../../../rti/protobuf/bin/protoc" ]; then
    protoc="../../../rti/protobuf/bin/protoc"
fi
echo "Using $protoc"
$protoc \
    --csharp_out=../Packages/com.inhumatesystems.rti/Runtime/Generated \
    --proto_path=../proto/ \
    ../proto/*.proto
