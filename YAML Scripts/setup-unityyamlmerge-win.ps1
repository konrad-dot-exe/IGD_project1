\
        # Run in PowerShell *as your user* once per machine.
        # Update <UNITY_VERSION> to match your installed editor (e.g., 2022.3.49f1).
        $unityMergePath = "C:\Program Files\Unity\Hub\Editor\<UNITY_VERSION>\Editor\Data\Tools\UnityYAMLMerge.exe"

        git config --global merge.unityyamlmerge.name "UnityYAMLMerge"
        git config --global merge.unityyamlmerge.driver "`"$unityMergePath`" merge -p %O %A %B %L"

        Write-Host "Registered UnityYAMLMerge at:" $unityMergePath
