#!/usr/bin/env bash
# Run once per machine. Update UNITY_VERSION to your installed version (e.g., 2022.3.49f1).
UNITY_VERSION="<UNITY_VERSION>"
UNITY_MERGE="/Applications/Unity/Hub/Editor/${UNITY_VERSION}/Unity.app/Contents/Tools/UnityYAMLMerge"

git config --global merge.unityyamlmerge.name "UnityYAMLMerge"
git config --global merge.unityyamlmerge.driver ""${UNITY_MERGE}" merge -p %O %A %B %L"

echo "Registered UnityYAMLMerge at: ${UNITY_MERGE}"
