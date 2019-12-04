using UnityEngine;
using TMPro;
using UnityEngine.UI;


/// <summary>The Multiprompt Singleton Monobehaviour Class</summary>
/// 
/// <remarks>
/// Singleton Class - one should be present on the active scene
/// Provides a way for the user to interact with program variables or execute small tasks
/// Shows on initialisation
/// Up to 3 configurable Buttons which each have a text and a callback
/// Has an optional text input
/// </remarks>
public class MultiPrompt : MonoBehaviour {

    /// <summary>The Multiprompt Singleton instance</summary>
    private static MultiPrompt _main;
	/// <summary>Gets the reference to the Multiprompt Singleton</summary>
    public  static MultiPrompt main {
        get {
            if (_main == null) _main = GameObject.FindObjectOfType<MultiPrompt>();
            return _main;
        }
    }

	/// <summary>The title text of the Multiprompt</summary>
    public TextMeshProUGUI title;
	/// <summary>The description text of the Multiprompt</summary>
    public TextMeshProUGUI description;

	/// <summary>The left button of the Multiprompt</summary>
    public Button button1;
	/// <summary>The middle button of the Multiprompt</summary>
    public Button button2;
	/// <summary>The right button of the Multiprompt</summary>
    public Button button3;
	/// <summary>The input field of the Multiprompt</summary>
    public TMP_InputField inputField;
	/// <summary>The main canvas of the Multiprompt</summary>
    public Canvas canvas;

	/// <summary>True upon exiting if the user pressed a button</summary>
    public bool userResponded;
	/// <summary>True upon exiting if the user pressed the cancel button</summary>
    public bool cancelled;

	/// <summary>The string entered into the input box</summary>
    string inputResponse;
    
	/// <summary>Initialises and shows the Multiprompt</summary>
	/// <param name="titleString">The title text to set the Multiprompt.</param>
	/// <param name="descriptionString">The description to set the Multiprompt.</param>
	/// <param name="useButtonSetup1">Whether to activate button1.</param>
	/// <param name="useButtonSetup2">Whether to activate button2.</param>
	/// <param name="useButtonSetup3">Whether to activate button3.</param>
	/// <param name="buttonSetup1">The setup for button1.</param>
	/// <param name="buttonSetup2">The setup for button2.</param>
	/// <param name="buttonSetup3">The setup for button3.</param>
	/// <param name="input">Whether to activate the input box.</param>
    private void Initialise(
        string titleString, 
        string descriptionString, 
        bool useButtonSetup1,
        bool useButtonSetup2,
        bool useButtonSetup3,
        ButtonSetup buttonSetup1,
        ButtonSetup buttonSetup2,
        ButtonSetup buttonSetup3,
        bool input=false
    ) {
        //Set the title text
        title.text = titleString;
        //Set the description text
        description.text = descriptionString;

        //Setup for Button 1
        if (useButtonSetup1) {
            button1.GetComponentInChildren<TextMeshProUGUI>().text = buttonSetup1.text;
            button1.onClick.AddListener(buttonSetup1.action);
            button1.gameObject.SetActive(true);
        } else {
            button1.gameObject.SetActive(false);
        }

        //Setup for Button 2
        if (useButtonSetup2) {
            button2.GetComponentInChildren<TextMeshProUGUI>().text = buttonSetup2.text;
            button2.onClick.AddListener(buttonSetup2.action);
            button2.gameObject.SetActive(true);
        } else {
            button2.gameObject.SetActive(false);
        }

        //Setup for Button 3
        if (useButtonSetup3) {
            button3.GetComponentInChildren<TextMeshProUGUI>().text = buttonSetup3.text;
            button3.onClick.AddListener(buttonSetup3.action);
            button3.gameObject.SetActive(true);
        } else {
            button3.gameObject.SetActive(false);
        }

        //Activate input box if necessary
        inputField.gameObject.SetActive(input);

        //Initialise feedback variables
        userResponded = false;
        cancelled = false;
        inputResponse = "";

        //Display the prompt
        Show();
    }

	/// <summary>Initialises and shows the Multiprompt</summary>
	/// <param name="titleString">The title text to set the Multiprompt.</param>
	/// <param name="descriptionString">The description to set the Multiprompt.</param>
	/// <param name="buttonSetup1">The setup for button1.</param>
	/// <param name="buttonSetup2">The setup for button2.</param>
	/// <param name="buttonSetup3">The setup for button3.</param>
	/// <param name="input">Whether to activate the input box.</param>
    public void Initialise(
        string titleString, 
        string descriptionString, 
        ButtonSetup buttonSetup1,
        ButtonSetup buttonSetup2,
        ButtonSetup buttonSetup3,
        bool input=false
    ) {
        Initialise(
            titleString,
            descriptionString,
            useButtonSetup1:true,
            useButtonSetup2:true,
            useButtonSetup3:true,
            buttonSetup1,
            buttonSetup2,
            buttonSetup3,
            input
        );
    }

	/// <summary>Initialises and shows the Multiprompt</summary>
	/// <param name="titleString">The title text to set the Multiprompt.</param>
	/// <param name="descriptionString">The description to set the Multiprompt.</param>
	/// <param name="buttonSetup1">The setup for button1.</param>
	/// <param name="buttonSetup2">The setup for button2.</param>
	/// <param name="input">Whether to activate the input box.</param>
    public void Initialise(
        string titleString, 
        string descriptionString, 
        ButtonSetup buttonSetup1,
        ButtonSetup buttonSetup2,
        bool input=false
    ) {
        Initialise(
            titleString,
            descriptionString,
            useButtonSetup1:true,
            useButtonSetup2:true,
            useButtonSetup3:false,
            buttonSetup1,
            buttonSetup2,
            new ButtonSetup(),
            input
        );
    }

	/// <summary>Initialises and shows the Multiprompt</summary>
	/// <param name="titleString">The title text to set the Multiprompt.</param>
	/// <param name="descriptionString">The description to set the Multiprompt.</param>
	/// <param name="buttonSetup1">The setup for button1.</param>
	/// <param name="input">Whether to activate the input box.</param>
    public void Initialise(
        string titleString, 
        string descriptionString, 
        ButtonSetup buttonSetup1,
        bool input=false
    ) {
        Initialise(
            titleString,
            descriptionString,
            useButtonSetup1:true,
            useButtonSetup2:false,
            useButtonSetup3:false,
            buttonSetup1,
            new ButtonSetup(),
            new ButtonSetup(),
            input
        );
    }

    public void Cancel() {
        userResponded = true;
        cancelled = true;
    }

    public void UserResponded() {
        userResponded = true;
    }

    public void Hide() {
        canvas.enabled = false;
    }

    public void Show() {
        canvas.enabled = true;
    }

    public string GetResponse() {
        return inputResponse;
    }
}

public struct ButtonSetup {
    public string text;
    public UnityEngine.Events.UnityAction action;

    public ButtonSetup(string text, UnityEngine.Events.UnityAction action) {
        this.text = text;
        this.action = action;
    }
}
