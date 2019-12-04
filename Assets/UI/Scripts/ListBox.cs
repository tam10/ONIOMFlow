using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ListBox : MonoBehaviour {

	public Transform contentHolder;

	[Header("Enabled File Color Block")]
	public ColorBlock enabledFileColorBlock;

	[Header("Disabled File Color Block")]
	public ColorBlock disabledFileColorBlock;

	[Header("Directory Color Block")]
	public ColorBlock directoryColorBlock;

	public void AddItem(string textValue, bool isFile, bool isEnabled, FileSelector fs) {
		
		ListItem item = PrefabManager.InstantiateListItem(contentHolder);

		//Remove edge
		item.edge.enabled = false;

		GameObject itemGo = item.gameObject;
		itemGo.name = textValue;
		item.text.text = textValue;

		Button button = item.GetComponent<Button>();
		if (isEnabled) {
			button.interactable = true;
			
			if (isFile) {
				button.onClick.AddListener(delegate {fs.SelectFile(textValue);});
				button.colors = enabledFileColorBlock;

			} else {
				button.onClick.AddListener(delegate {fs.ChangeDirectory(textValue);});
				button.colors = directoryColorBlock;
			}
			
		} else {
			button.interactable = false;
			button.colors = disabledFileColorBlock;
		}
	}

	public void Clear() {
		foreach (Transform child in contentHolder) {
			GameObject.Destroy(child.gameObject);
		}
	}
}
