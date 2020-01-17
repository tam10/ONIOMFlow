import xml.etree.ElementTree as ET
import os
import sys

amino_acid_names = [
    'GLU', 
    'PRO', 
    'HIS_C', 
    'ALA', 
    'CYX_C', 
    'HIE_N',
    'TYR_N',
    'LEU_C',
    'HIP_N',
    'GLY_C',
    'ILE',
    'CYS',
    'GLH',
    'CYS_C',
    'VAL_N',
    'ASH',
    'MET_N',
    'VAL',
    'GLN_N',
    'THR_N',
    'ILE_C',
    'GLU_C',
    'PHE_N',
    'ALA_C',
    'ASP_C',
    'TRP_N',
    'HID_N',
    'CYX',
    'CYM',
    'THR',
    'ARG_C',
    'LYS_C',
    'PRO_C',
    'ASN_C',
    'SER',
    'SER_C',
    'ARG',
    'GLN',
    'HID',
    'HID_C',
    'TRP_C',
    'LYN',
    'ASP_N',
    'ALA_N',
    'PHE_C',
    'LEU',
    'ASN',
    'GLU_N',
    'ILE_N',
    'TYR',
    'THR_C',
    'GLN_C',
    'SER_N',
    'ASN_N',
    'PHE',
    'PRO_N',
    'LYS_N',
    'LYS',
    'ARG_N',
    'HIP',
    'HIE',
    'GLY_N',
    'HIP_C',
    'LEU_N',
    'TYR_C',
    'HIE_C',
    'CYX_N',
    'HIS_N',
    'ASP',
    'MET',
    'MET_C',
    'GLY',
    'TRP',
    'VAL_C',
    'HIS',
    'CYS_N',
]

charge_dict = {
    "N": {
        2: -1,
        3: 0,
        4: 1
    },
    "NA": {
        2: -1,
        3: 0
    },
    "NB": {
        2: 0,
        3: 1
    },
    "NC": {
        2: 0,
        3: 1
    },
    "N2": {
        2: -1,
        3: 0,
        4: 1
    },
    "N3": {
        3: 0,
        4: 1
    },
    "O": {
        1: 0,
        2: 1
    },
    "OH": {
        1: -1,
        2: 0
    },
    "O2": {
        1: -0.5,
        2: 0.5
    },
    "SH": {
        1: -1,
        2: 0
    }
}

electrons = {
    "C":  6,
    "CA": 6,
    "CB": 6,
    "CC": 6,
    "CD": 6,
    "CK": 6,
    "CN": 6,
    "CR": 6,
    "CT": 6,
    "CV": 6,
    "CW": 6,
    "C*": 6,
    "H": 1,
    "H1": 1,
    "H2": 1,
    "H3": 1,
    "H4": 1,
    "H5": 1,
    "HP": 1,
    "HA": 1,
    "HC": 1,
    "HO": 1,
    "HS": 1,
    "N": 7,
    "N2": 7,
    "N3": 7,
    "NA": 7,
    "NB": 7,
    "N*": 7,
    "O": 8,
    "O2": 8,
    "OH": 8,
    "S": 16,
    "SH": 16,
}

aromatics = {
    "C": 1,
    "CA": 1,
    "CB": 1,
    "CC": 1,
    "CD": 1,
    "CK": 1,
    "CN": 1,
    "CR": 1,
    "CV": 1,
    "CW": 1,
    "C*": 1,
    "NB": 1,
    "O2": 0.5,
    "O": 1
}

class Atom(object):
    def __init__(self, amber, pdb, num_neighbours):
        self.amber = amber
        self.pdb = pdb
        self.num_neighbours = num_neighbours

class AminoAcid(object):
    def __init__(self, fn):

        tree = ET.parse(fn)
        root = tree.getroot()

        self.atoms = []

        self.total_charge = 0

        self.name = fn.split("/")[-1].split(".")[0]

        for residueX in root.iter("residue"):
            #if residueX.attrib["name"] not in ["ACE", "NME", "CRO"]:
            #    continue
            self.total_charge += float(residueX.attrib["charge"])
            for atomX in residueX:
                pdb = atomX.attrib["ID"]
                amber = atomX.attrib["amber"]
                num_neighbours = len(atomX.find("bonds").text.split(","))

                self.atoms.append(Atom(amber, pdb, num_neighbours))
    
    def predict_charge(self):

        predicted_charge = 0.
        aromatic_count = 0

        for atom in self.atoms:

            if atom.amber in aromatics:
                aromatic_count += aromatics[atom.amber]

            if atom.pdb == " O  " and atom.amber == "O":
                continue

            if atom.pdb == " N  " and atom.amber == "N":
                continue

            if atom.pdb == " C  " and atom.amber == "C":
                continue

            try:
                neighbour_charge_dict = charge_dict[atom.amber]
            except:
                continue

            predicted_charge += neighbour_charge_dict[atom.num_neighbours]

        name_mod_charge = 0#-1 if self.name.endswith("_N") else 0

        return (predicted_charge + (aromatic_count % 2) + name_mod_charge, aromatic_count)

def process_amino_acid(amino_acid : AminoAcid, print_all : bool):

    predicted_charge, aromatic_count = amino_acid.predict_charge()
    actual_charge = amino_acid.total_charge
    

    multiplicity = 1 if (sum([electrons[a.amber] for a in amino_acid.atoms]) + predicted_charge) % 2 == 0 else 2

    charge_contributions = " ".join([
        "{:2s}".format(a.amber) + (
            "+" if charge_dict[a.amber][a.num_neighbours] > 0 
            else "-" if charge_dict[a.amber][a.num_neighbours] < 0 
            else " "
        )
        for a in amino_acid.atoms 
        if a.amber in charge_dict
    ])

    if print_all or (int(predicted_charge) != int(actual_charge)):
        print("{0:5s} | {1:4.1f} {2:4.1f} {3:4.1f} | {4:1d} | {5}".format(
            amino_acid.name, 
            predicted_charge, 
            actual_charge, 
            aromatic_count, 
            multiplicity,
            charge_contributions
        ))


if __name__ == "__main__":

    if (len(sys.argv) > 1):

        fn = sys.argv[1]

        aa = AminoAcid(fn)

        process_amino_acid(aa, True)
        exit(0)



    directory = os.path.dirname(os.path.realpath(__file__))

    amino_acids = {
        amino_acid_name : AminoAcid(os.path.join(directory, amino_acid_name + ".xat.txt"))
        for amino_acid_name in amino_acid_names
    }

    for amino_acid_name in amino_acid_names:
        amino_acid = AminoAcid(os.path.join(directory, amino_acid_name + ".xat.txt"))

        process_amino_acid(amino_acid, False)

