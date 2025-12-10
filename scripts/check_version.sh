#!/bin/bash

# Checks that, for releases, the tag version corresponds to the version in package.json etc

version=$1 ; shift
if [ -z "$version" ]; then 
    echo "usage: $0 <version>"
    exit 2
fi

if grep -q "\"version\": \"${version}\"" Packages/com.inhumatesystems.rti/package.json ;\
    grep -q "Version = \"${version}\"" Packages/com.inhumatesystems.rti/Runtime/RTIConnection.cs ; then
    echo "Version check ok"
else
    echo "Version check fail - update package.json and RTIConnection.cs to version ${version}"
    exit 1
fi
