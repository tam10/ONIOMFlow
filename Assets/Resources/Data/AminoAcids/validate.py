import os
import itertools as it
import numpy as np
import numba
import copy

amino_acid_names = [
    'GLU', 
    'PRO', 
    'HIS_C', 
    'ALA', 
    'CYX_C', 
    'HIE_N',
    #'TYR_N',
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
    #'LYN',
    'ASP_N',
    'ALA_N',
    'PHE_C',
    'LEU',
    'ASN',
    'GLU_N',
    'ILE_N',
    #'TYR',
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
    #'TYR_C',
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
    "C": 1,
    "CA": 0,
    "CB": 0,
    "CC": 0,
    "CD": 0,
    "CK": 0,
    "CN": 0,
    "CR": -1,
    "CT": 0,
    "CV": 0,
    "CW": 0,
    "C*": 0,
    "H": 1,
    "H1": 0,
    "H2": 0,
    "H3": 0,
    "H4": 0,
    "H5": 1,
    "HP": 0,
    "HA": 0,
    "HC": 0,
    "HO": 1,
    "HS": 1,
    "N": -1,
    "N2": -1,
    "N3": -2,
    "NA": -1,
    "NB": 0,
    "N*": 0,
    "O": -1,
    "O2": -1,
    "OH": -1,
    "HW": 1,
    "OW": -2,
    "S": 0,
    "SH": -1,
    "IM": 2,
    "CL": -1,
    "CU": 2,
    "FE": 2,
    "MG": 2,
    "QC": 1,
    "QK": 1,
    "QL": 1,
    "QN": 1,
    "QR": 1,
}

M4_P4 = [-4, -3, -2, -1, 0, 1, 2, 3, 4]
M3    = [-3]
M3_Z  = [-3, -2, -1, 0]
M3_P1 = [-3, -2, -1, 0, 1]
M3_P5 = [-3, -2, -1, 0, 1, 2, 3, 4, 5]
M2    = [-2]
M2_M1 = [-2, -1]
M2_Z  = [-2, -1, 0]
M2_P2 = [-2, -1, 0, 1, 2]
M1    = [-1]
M1_Z  = [-1, 0]
M1_P1 = [-1, 0, 1]
M1_P2 = [-1, 0, 1, 2]
Z     = [0]
Z_P1  = [0, 1]
Z_P2  = [0, 1, 2]
P1    = [1]
P2    = [2]

possible_charges = {
    "C":  P1,
    "CA": Z,
    "CB": Z,
    "CC": Z,
    "CD": M1_P1,
    "CK": M1_P1,
    "CN": Z,
    "CR": M1_P1,
    "CT": Z,
    "CV": M1_P1,
    "CW": M1_P1,
    "C*": Z,
    "H": P1,
    "H1": Z,
    "H2": Z_P1,
    "H3": Z_P1,
    "H4": Z_P1,
    "H5": Z_P1,
    "HP": Z_P1,
    "HA": Z,
    "HC": Z,
    "HO": Z_P1,
    "HS": Z_P1,
    "N": M2_Z,
    "N2": M2_Z,
    "N3": M2_Z,
    "NA": M1_Z,
    "NB": M1_Z,
    "O": M2_Z,
    "O2": M1,
    "OH": M2_M1,
    "S": Z,
    "SH": M1,
}

flexible_charges_dict = {
    "C":  M4_P4,
    "CA": M4_P4,
    "CB": M4_P4,
    "CC": M4_P4,
    "CD": M4_P4,
    "CK": M4_P4,
    "CN": M4_P4,
    "CR": M4_P4,
    "CT": M4_P4,
    "CV": M4_P4,
    "CW": M4_P4,
    "C*": M4_P4,
    "H": P1,
    "H1": M1_P1,
    "H2": M1_P1,
    "H3": M1_P1,
    "H4": M1_P1,
    "H5": M1_P1,
    "HP": M1_P1,
    "HA": M1_P1,
    "HC": M1_P1,
    "HO": P1,
    "HS": M1_P1,
    "N": M3_P5,
    "N2": M3_P5,
    "N3": M3_P5,
    "NA": M3_P5,
    "NB": M3_P5,
    "O": M2_Z,
    "O2": M2_Z,
    "OH": M2_Z,
    "S": M2_Z,
    "SH": M2_Z,
}


product = 1
for v in flexible_charges_dict.values():
    product *= len(v)
print("Count: {0}".format(product))


amber_names = sorted(flexible_charges_dict)
num_amber_names = len(amber_names)
flexible_charges = [flexible_charges_dict[a] for a in amber_names]

class AminoAcid(object):
    def __init__(self, fn):

        self.charge = 0
        self.ambers = []

        self.amber_count = np.zeros(shape=num_amber_names, dtype=int)

        for line in open(fn):
            if "<residue " in line:
                self.charge = int(float(line.split("charge=\"")[1].split("\"")[0]))

            sl = line.split("amber=\"")
            if len(sl) > 1:
                amber = sl[1].split("\"")[0]
                self.ambers.append(amber)

                self.amber_count[amber_names.index(amber)] += 1

    def get_charge(self, amber_charges : list):
        return _get_charge(amber_charges, self.amber_count, num_amber_names)

    def get_error(self, amber_charges : list):
        return abs(self.get_charge(amber_charges) - self.charge)

@numba.jit(signature_or_function="int64(int64[:], int64[:], int64)", nopython=True)
def _get_charge(amber_charges, amber_count, length):
    charge = 0
    for i in range(length):
        charge += amber_charges[i] * amber_count[i]
    return charge

@numba.jit(signature_or_function=("int64(int64[:], int64[:], int64[:,:], int64, int64)"), nopython=True)
def _get_combination_score(amber_charges, amino_acid_charges, amber_counts, num_ambers, num_amino_acids):
    score = 0

    # Loop amino acids
    for i in range(num_amino_acids):

        # Get amino acid charge
        target_charge = amino_acid_charges[i]

        result_aa_charge = 0
        # Loop AMBER names in amino acid AMBER count
        for j in range(num_ambers):
            result_aa_charge += amber_counts[i,j] * amber_charges[j]

        score += abs(target_charge - result_aa_charge)

    return score


def validate_file(fn):
    amber_charge = 0
    ambers = []
    for line in open(fn):
        if "<residue " in line:
            charge = int(float(line.split("charge=\"")[1].split("\"")[0]))

        sl = line.split("amber=\"")
        if len(sl) > 1:
            amber = sl[1].split("\"")[0]
            ambers.append(amber)
            amber_charge += charge_dict[amber]

    #if amber_charge == charge - 2:
    if amber_charge != charge:
        print(
            "{0:5s} {1:2d} {2}".format(
                fn.split("/")[-1].split(".")[0], 
                amber_charge - charge,
                ambers
            )
        )

#v = (1, 0, 0, 0, 0, -1, -1, -1, 0, 0, 0, 2, -1, -1, 2, 0, 0, 0, 0, 1, 1, 0, 0, 0, 1, 1, 0, 2, 2, 0, 0, 0, 0, -1, -1, -1, 0, 0, 1, 1, 1, 1, 1, 0, -1)

v = np.array([1, 0, 0, 0, 0, -1, -1, 0, -1, 0, -1, -1, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 1, -1, -1, -2, 0, 0, -1, -1, -1, 0, -1])
length = len(v)
v = np.array([np.random.randint(-2,2) for i in range(length)])
#charge_dict = {k:v for k,v in zip(amber_names,v)}

def mutate_gene(combination, index):

    possible_values = flexible_charges[index]

    combination[index] = np.random.choice(possible_values) 


def mutate(combination, rate, length):

    for i in range(rate):
        index = np.random.randint(length)

        mutate_gene(combination, index)

def breed(combination0, combination1, num_offspring, rate, length):
    offspring = []

    for i in range(num_offspring):

        new_combination = np.array([
            np.random.choice(
                (combination0[i], combination1[i])
            ) for i in range(length)
        ])

        mutate(new_combination, rate, length)

        offspring.append(new_combination)
    return offspring

def get_best(offspring, num_to_get):
    zipped = [
        [
            _get_combination_score(
                o, 
                amino_acid_charges, 
                amber_counts, 
                num_amber_names, 
                num_amino_acids
            ), 
            o
        ] for o in offspring
    ]
    return [o[1] for o in sorted(zipped, key=lambda x: x[0])[:num_to_get]]

if __name__ == "__main__":
    size = 24

    directory = os.path.dirname(os.path.realpath(__file__))

    amino_acids = {
        amino_acid_name : AminoAcid(os.path.join(directory, amino_acid_name + ".xat.txt"))
        for amino_acid_name in amino_acid_names
    }

    num_amino_acids = len(amino_acids)

    amino_acid_charges = np.array([amino_acids[an].charge for an in amino_acid_names], dtype=int)
    amber_counts = np.array([amino_acids[an].amber_count for an in amino_acid_names], dtype=int)
    
    
    current_set = breed(v, v, size, 1, length)


    #current_set = [
    #   #           C   C*  CA  CB  CC  CD  CK  CN  CR  CT CV  CW   H  H1  H2  H3  H4  H5  HA  HC  HO  HP  HS  N  N2  N3  NA  NB   O  O2  OH   S  SH 
    #    np.array([ 1,  4,  0,  0,  4, -2, -4, -2,  0,  0, -2, -2,  0,  0,  1,  1,  1, 0,  0,  0,  0,  0,  1,  0,  0,  1, -1, -2, -1, -1,  0,  0, -1]), 
    #    np.array([ 1,  2,  0,  0,  4, -2,  0,  0,  0,  0, -2, -2,  0,  0,  0,  0,  1, 0,  0,  0,  0,  0,  1,  0,  0,  1, -1, -2, -1, -1,  0,  0, -1]), 
    #    np.array([ 1,  2,  0,  0,  4,  4, -4,  0,  0,  0, -2, -2,  0,  0,  0,  0,  1, 0,  0,  0,  0,  0,  1,  0,  0,  1, -1, -2, -1, -1,  0,  0, -1]), 
    #    np.array([ 1,  2,  0, -4,  4, -3,  3,  4,  0,  0, -2, -2,  0,  0,  0,  0,  1, 0,  0,  0,  0,  0,  1,  0,  0,  1, -1, -2, -1, -1,  0,  0, -1])
    #]


    current_set = [
       #           C   C*  CA  CB  CC  CD  CK  CN  CR  CT CV  CW   H  H1  H2  H3  H4  H5  HA  HC  HO  HP  HS  N  N2  N3  NA  NB   O  O2  OH   S  SH 
        np.array([ 1,  4,  0,  0,  4, -2, -4, -2,  0,  0, -2, -2,  1,  0,  1,  1,  1, 0,  0,  0,  0,  0,  1,  -1,  0,  1, -1, -2, -1, -1, -1, 0, -1]), 
        np.array([ 1,  2,  0,  0,  4, -2,  0,  0,  0,  0, -2, -2,  1,  0,  0,  0,  1, 0,  0,  0,  0,  0,  1,  -1,  0,  1, -1, -2, -1, -1, -1, 0, -1]), 
        np.array([ 1,  2,  0,  0,  4,  4, -4,  0,  0,  0, -2, -2,  1,  0,  0,  0,  1, 0,  0,  0,  0,  0,  1,  -1,  0,  1, -1, -2, -1, -1, -1, 0, -1]), 
        np.array([ 1,  2,  0, -4,  4, -3,  3,  4,  0,  0, -2, -2,  1,  0,  0,  0,  1, 0,  0,  0,  0,  0,  1,  -1,  0,  1, -1, -2, -1, -1, -1, 0, -1])
    ]


    steps = 10000000

    best_score = 5000
    try:
        for step in range(steps):

            best = get_best(current_set, 4)

            current_set = \
                breed(best[0], best[0], 2, 3, length) + \
                breed(best[1], best[1], 2, 3, length) + \
                breed(best[2], best[2], 2, 3, length) + \
                breed(best[3], best[3], 2, 3, length) + \
                breed(best[0], best[1], 2, 6, length) + \
                breed(best[0], best[2], 2, 6, length) + \
                breed(best[0], best[3], 2, 6, length) + \
                breed(best[1], best[2], 2, 6, length) + \
                breed(best[1], best[3], 2, 6, length) + \
                breed(best[2], best[3], 2, 6, length) + \
                best

            current_score = min([
                _get_combination_score(
                    o, 
                    amino_acid_charges, 
                    amber_counts, 
                    num_amber_names, 
                    num_amino_acids
                )
                for o in current_set
            ])
            
            if current_score < best_score:
                best_score = current_score
                print(best_score)

                if current_score == 0:
                    break
    except KeyboardInterrupt:
        pass

    print([
            [
                _get_combination_score(
                    o, 
                    amino_acid_charges, 
                    amber_counts, 
                    num_amber_names, 
                    num_amino_acids
                ),
                o
            ]
            for o in current_set
        ])
    print(get_best(current_set, 4))
        
    best = get_best(current_set, 1)[0]
    print({amber_names[i]: best[i] for i in range(length)})

    error = 0
    for fn, aa in amino_acids.items():
        tc = aa.charge
        c = aa.get_charge(np.array(list(best)))
        if (tc != c):
            error += abs(tc - c)
            print("{0:5s} {1:2d} {2:2d} {3}".format( fn, tc, c, {a:best[amber_names.index(a)] for a in aa.ambers}))
    print(error)


exit(0)
if __name__ == "__main__":


    #error = 0
    #for fn, aa in amino_acids.items():
    #    tc = aa.charge
    #    c = aa.get_charge(np.array(list(v)))
    #    if (tc != c):
    #        error += abs(tc - c)
    #        print("{0:5s} {1:2d} {2:2d} {3}".format( fn, tc, c, {a:v[amber_names.index(a)] for a in aa.ambers}))
    #print(error)
    #exit(0)

    product = 1
    for v in possible_charges.values():
        product *= len(v)
    print("Count: {0}".format(product))

    amber_names = sorted(possible_charges)
    num_amino_acids = len(amino_acids)

    amino_acid_charges = np.array([amino_acids[an].charge for an in amino_acid_names], dtype=int)
    amber_counts = np.array([amino_acids[an].amber_count for an in amino_acid_names], dtype=int)

    count = -1
    min_error = 40
    for v in it.product(*(possible_charges[name] for name in amber_names)):

        count += 1

        error = _get_combination_score(np.array(v, dtype=int), amino_acid_charges, amber_counts, num_amber_names, num_amino_acids)

        if error < min_error:
            min_error = error
            print("Best ({0:3d}): {1}".format(min_error, v))

        if error < 7:
            with open("solutions.txt", "w") as f:
                f.write("{0}\n{1}\n".format(error, v))

        if count % 1000000 == 0:
            print(count)

    t = np.array([-1, 0, 0, 0, 0, -1, -1, -1, 0, -1, 0, 2, -1, -1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2, 2, -2, -2, -2, -1, -1, -2, -1, -1, 0, 1, 1, 1, 1, 1, 0, -1])

    charge_dict = {
        k:v
        for k,v
        in zip(amber_names, t)
    }

    #print(len(it.product(*possible_charges.values())))


    #for fn in os.listdir(directory):
    #    if fn.endswith("xat.txt"):
    #        validate_file(os.path.join(directory, fn))
