using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.IO;

public class ProjectBuilder : MonoBehaviour {
    
    void Start() {
        StartCoroutine(LoadProject());
    }

    IEnumerator LoadProject() {
        
        ProjectSelector projectSelector = ProjectSelector.main;

        yield return projectSelector.Initialise();

        while (!projectSelector.userResponded) {
            yield return null;
        }

        string projectPath = projectSelector.projectPath;
        
        projectSelector.Hide();

        if (projectSelector.cancelled) {
            Application.Quit();
            yield break;
        }

        SettingsBuilder settingsBuilder = SettingsBuilder.main;
        
        yield return settingsBuilder.Initialise();


        CreateDirectory(projectPath);

        string settingsDirectory = "Settings";
        string settingsPath = Path.Combine(projectPath, settingsDirectory);

        CreateDirectory(settingsPath);
        CreateFile(settingsDirectory, "ProjectSettings", settingsPath, "ProjectSettings.xml");
        CreateFile(settingsDirectory, "Tasks", settingsPath, "Tasks.xml");
        CreateFile(settingsDirectory, "Atoms", settingsPath, "Atoms.xml");
        CreateFile(settingsDirectory, "Graphics", settingsPath, "Graphics.xml");
        CreateFile(settingsDirectory, "Flow", settingsPath, "Flow.xml");
        CreateFile(settingsDirectory, "ResidueTable", settingsPath, "ResidueTable.xml");

        string dataDirectory = "Data";
        string dataPath = Path.Combine(projectPath, dataDirectory);

        CreateDirectory(dataPath);
        CreateFile(dataDirectory, "Bonds", dataPath, "Bonds.xml");
        CreateFile(dataDirectory, "StandardResidues", dataPath, "StandardResidues.xml");
        CreateFile(dataDirectory, "GaussianMethods", dataPath, "GaussianMethods.xml");
        CreateFile(dataDirectory, "amberToElement", dataPath, "amberToElement.csv");
        CreateFile(dataDirectory, "pdbToElement", dataPath, "pdbToElement.csv");

        yield return Settings.Initialise(projectPath);
        yield return Data.Initialise();

        settingsBuilder.Hide();
        SceneManager.LoadScene("Overview");

    }

    void CreateDirectory(string path) {
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }
    }

    void CreateFile(string resourceDirectory, string resourceName, string path, string fileName) {
        
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
            SettingsBuilder.AddProgressText(string.Format("Created Directory: {0}", path));
        }

        string resourcePath = Path.Combine(resourceDirectory, resourceName);
        string fullPath = Path.Combine(path, fileName);
        
        if (!File.Exists(fullPath)) {
            TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null) {
                throw new System.Exception(string.Format("Cannot find TextAsset: {0}", resourcePath));
            }
            File.WriteAllText(fullPath, textAsset.text);
            SettingsBuilder.AddProgressText(string.Format("Built Settings file: {0}", fileName));
        }
    }
}
