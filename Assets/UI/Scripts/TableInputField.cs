using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using RP = Constants.ResidueProperty;

public class TableInputField : TableField
{
    
    public TMP_InputField tmpInputField;

    public override void GetValue() {
        switch (residueProperty) {
            case (RP.CHAINID):
                tmpInputField.text = residue.chainID;
                break;
            case (RP.CHARGE):
                tmpInputField.text = string.Format("{0:0.00}", residue.GetCharge());
                break;
            case (RP.RESIDUE_NAME):
                tmpInputField.text = residue.residueName;
                break;
            case (RP.RESIDUE_NUMBER):
                tmpInputField.text = residue.number.ToString();
                break;
            case (RP.SIZE):
                tmpInputField.text = residue.size.ToString();
                break;
            case (RP.STATE):
                tmpInputField.text = residue.state.ToString();
                break;
        }
    }

}
