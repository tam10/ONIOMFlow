using System.Collections;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.IO;

public class ProjectBuilder : MonoBehaviour {

    public string projectPath;
    public string settingsDirectory;
    public string settingsPath;
    public string dataDirectory;
    public string dataPath;
    public string recipeDirectory;
    public string recipePath;

    public string projectFile = ".project";
    
    void Start() {
        StartCoroutine(LoadProject());
    }

    IEnumerator LoadProject() {

        yield return GetProjectPath();
        
        SettingsBuilder settingsBuilder = SettingsBuilder.main;
        yield return settingsBuilder.Initialise();

        GetSettingsFiles();
        GetDataFiles();
        GetRecipeFiles();

        yield return Settings.Initialise(projectPath);
        yield return Data.Initialise();

        settingsBuilder.Hide();
        SceneManager.LoadScene("Overview");

    }

    IEnumerator GetProjectPath() {

        projectPath = "";
        
        ProjectSelector projectSelector = ProjectSelector.main;

        yield return projectSelector.Initialise();

        while (string.IsNullOrWhiteSpace(projectPath)) {

            while (!projectSelector.userResponded) {
                yield return null;
            }

            if (projectSelector.cancelled) {
                #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                #else
                    Application.Quit();
                #endif
                yield break;
            }

            string path = projectSelector.projectPath;
            string projectFilePath = Path.Combine(path, projectFile);

            projectSelector.userResponded = false;

            if (string.IsNullOrWhiteSpace(path)) {
                yield return null;
            } else if (!Directory.Exists(path) || !File.Exists(projectFilePath)) {

                bool createNew = false;

                MultiPrompt multiPrompt = MultiPrompt.main;

                multiPrompt.Initialise(
                    "Create New Project",
                    string.Format("Create a new project in '{0}'?", path),
                    new ButtonSetup(text:"Confirm", action:() => createNew = true),
                    new ButtonSetup(text:"Cancel", action:() => createNew = false)
                );

                while (!multiPrompt.userResponded) {
				    yield return null;
                }

                multiPrompt.Hide();

                if (createNew) {
                    projectPath = path;
                    CreateDirectory(projectPath);
                    File.Create(projectFilePath);
                    File.SetAttributes(projectFilePath, File.GetAttributes(projectFilePath) | FileAttributes.Hidden);

                    break;
                }

            } else {
                projectPath = path;
                break;
            }
        }


        projectSelector.Hide();

    }

    void GetSettingsFiles() {

        settingsDirectory = "Settings";
        settingsPath = Path.Combine(projectPath, settingsDirectory);

        CreateDirectory(settingsPath);
        CreateFile(settingsDirectory, "ProjectSettings", settingsPath, "ProjectSettings.xml");
        CreateFile(settingsDirectory, "Tasks", settingsPath, "Tasks.xml");
        CreateFile(settingsDirectory, "Atoms", settingsPath, "Atoms.xml");
        CreateFile(settingsDirectory, "Graphics", settingsPath, "Graphics.xml");
        CreateFile(settingsDirectory, "Flow", settingsPath, "Flow.xml");
        CreateFile(settingsDirectory, "ResidueTable", settingsPath, "ResidueTable.xml");
    }

    void GetDataFiles() {
        dataDirectory = "Data";
        dataPath = Path.Combine(projectPath, dataDirectory);

        CreateDirectory(dataPath);
        CreateFile(dataDirectory, "Bonds", dataPath, "Bonds.xml");
        CreateFile(dataDirectory, "StandardResidues", dataPath, "StandardResidues.xml");
        CreateFile(dataDirectory, "GaussianMethods", dataPath, "GaussianMethods.xml");
        CreateFile(dataDirectory, "amberToElement", dataPath, "amberToElement.csv");
        CreateFile(dataDirectory, "pdbToElement", dataPath, "pdbToElement.csv");
        CreateFile(dataDirectory, "SASA", dataPath, "SASA.txt");
    }

    void GetRecipeFiles() {
        recipeDirectory = "Recipes";
        recipePath = Path.Combine(projectPath, recipeDirectory);

        CreateDirectory(recipePath);
        CreateFile(recipeDirectory, "resp", recipePath, "resp.xml");
        CreateFile(recipeDirectory, "mutate", recipePath, "mutate.xml");
        CreateFile(recipeDirectory, "mutateAllDouble", recipePath, "mutateAllDouble.xml");
        CreateFile(recipeDirectory, "mutateAll", recipePath, "mutateAll.xml");
        CreateFile(recipeDirectory, "mutateCompare", recipePath, "mutateCompare.xml");
    }

    void CreateDirectory(string path) {
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }
    }

    void CreateFile(string resourceDirectory, string resourceName, string path, string fileName) {
        
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
            SettingsBuilder.AddProgressText(string.Format("Created Directory: {0}{1}", path, FileIO.newLine));
        }

        string resourcePath = Path.Combine(resourceDirectory, resourceName);
        string fullPath = Path.Combine(path, fileName);
        
        if (!File.Exists(fullPath)) {
            TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
            if (textAsset == null) {
                throw new System.Exception(string.Format("Cannot find TextAsset: {0}", resourcePath));
            }
            File.WriteAllText(fullPath, textAsset.text);
            SettingsBuilder.AddProgressText(string.Format("Built Settings file: {0}{1}", fileName, FileIO.newLine));
        }
    }
}
