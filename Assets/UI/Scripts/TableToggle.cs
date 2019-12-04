using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RP = Constants.ResidueProperty;

public class TableToggle : TableField {
    public Toggle toggle;

    public override void GetValue() {
        switch (residueProperty) {
            case (RP.PROTONATED):
                toggle.isOn = residue.protonated;
                break;
            case (RP.STANDARD):
                toggle.isOn = residue.standard;
                break;
        }
    }
}
