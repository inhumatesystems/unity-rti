#!/bin/bash -e

cd "$(dirname $0)/.."

if [ -e protobuf/bin/protoc ]; then
    exit 0
fi

mkdir -p protobuf
cd protobuf
curl -L https://github.com/protocolbuffers/protobuf/releases/download/v23.3/protoc-23.3-linux-x86_64.zip -o protoc.zip
unzip protoc.zip

