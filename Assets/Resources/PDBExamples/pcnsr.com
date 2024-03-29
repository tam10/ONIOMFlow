﻿%nprocshared=1
%mem=4000MB
%chk=/Users/tristanmackenzie/Unity/ONIOM Flow/Assets/Resources/PDBExamples/pcnsr.chk
#N AMBER=softfirst geom=connectivity  

Title

0 1 
C-C-0.5972(PDBName=C,ResName=ACE,ResNum=64)        0        11.6420000000        68.0570000000       -12.1470000000 L
C-CT--0.3662(PDBName=CH3,ResName=ACE,ResNum=64)    0        13.1653000000        67.9150000000       -12.1597000000 L
H-HC-0.1123(PDBName=1HH3,ResName=ACE,ResNum=64)    0        13.4732000000        67.0413000000       -12.7341000000 L
H-HC-0.1123(PDBName=2HH3,ResName=ACE,ResNum=64)    0        13.5951000000        68.8126000000       -12.6043000000 L
H-HC-0.1123(PDBName=3HH3,ResName=ACE,ResNum=64)    0        13.5145000000        67.8164000000       -11.1319000000 L
O-O--0.5679(PDBName=O,ResName=ACE,ResNum=64)       0        10.9347000000        67.2430000000       -12.7366000000 L
C-CK--0.0208(PDBName=C,ResName=CRA,ResNum=65)      0         9.4310000000        69.1470000000        -9.7490000000 L
C-C--0.0374(PDBName=C1,ResName=CRA,ResNum=65)      0         8.1310000000        70.9000000000        -9.8520000000 L
C-C-0.1418(PDBName=C2,ResName=CRA,ResNum=65)       0         7.2390000000        68.9140000000       -12.4120000000 L
C-CT-0.2493(PDBName=CA,ResName=CRA,ResNum=65)      0        10.2370000000        67.9200000000       -10.1720000000 L
C-CC-0.0473(PDBName=CA1,ResName=CRA,ResNum=65)     0         8.7290000000        70.7670000000        -8.4790000000 L
C-CT-0.1109(PDBName=CA2,ResName=CRA,ResNum=65)     0         8.4180000000        69.7700000000       -11.9750000000 L
C-CT-0.0195(PDBName=CB,ResName=CRA,ResNum=65)      0        10.7230000000        67.0850000000        -8.9840000000 L
C-CD--0.3418(PDBName=CB1,ResName=CRA,ResNum=65)    0         8.4910000000        71.7110000000        -7.5010000000 L
C-CA-0.3841(PDBName=CD1,ResName=CRA,ResNum=65)     0         9.8530000000        70.8050000000        -5.6690000000 L
C-CA--0.1768(PDBName=CD2,ResName=CRA,ResNum=65)    0         8.6220000000        72.8290000000        -5.3480000000 L
C-CA-0.6097(PDBName=CE1,ResName=CRA,ResNum=65)     0        10.3480000000        70.9000000000        -4.3520000000 L
C-CA-0.1768(PDBName=CE2,ResName=CRA,ResNum=65)     0         9.0980000000        72.9350000000        -4.0330000000 L
C-CA--0.2288(PDBName=CG,ResName=CRA,ResNum=65)     0         8.9870000000        71.7760000000        -6.1800000000 L
C-CA-0.4486(PDBName=CZ,ResName=CRA,ResNum=65)      0         9.9540000000        71.9730000000        -3.5430000000 L
H-H--0.4945(PDBName=H,ResName=CRA,ResNum=65)       0        12.0081000000        69.1755000000       -10.4452000000 L
H-H1--0.5487(PDBName=HA,ResName=CRA,ResNum=65)     0         9.7521000000        67.2499000000       -10.9565000000 L
H-H1--0.6093(PDBName=HA1,ResName=CRA,ResNum=65)    0         8.4180000000        70.7397000000       -12.5743000000 L
H-H1--0.5748(PDBName=HA2,ResName=CRA,ResNum=65)    0         9.3403000000        69.2000000000       -12.3273000000 L
H-HA-0.0981(PDBName=HB,ResName=CRA,ResNum=65)      0         7.6910000000        72.5205000000        -7.6862000000 L
H-H1--0.5283(PDBName=HB1,ResName=CRA,ResNum=65)    0        11.6453000000        66.5150000000        -9.3363000000 L
H-H1--0.4326(PDBName=HB2,ResName=CRA,ResNum=65)    0        10.9082000000        67.8850000000        -8.1745000000 L
H-HA-0.1848(PDBName=HD,ResName=CRA,ResNum=65)      0         7.9519000000        73.6135000000        -5.8329000000 L
H-HA-0.1188(PDBName=HD1,ResName=CRA,ResNum=65)     0        10.3379000000        70.1349000000        -6.4535000000 L
H-HA-0.0705(PDBName=HE,ResName=CRA,ResNum=65)      0         8.6131000000        73.6051000000        -3.2485000000 L
H-HA-0.3048(PDBName=HE1,ResName=CRA,ResNum=65)     0        11.0181000000        70.1155000000        -3.8671000000 L
H-HO-0.0922(PDBName=HG,ResName=CRA,ResNum=65)      0         8.9569000000        66.7736000000        -7.8303000000 L
H-HO-0.397(PDBName=HH1,ResName=CRA,ResNum=65)      0        10.3790000000        71.2278000000        -1.7140000000 L
N-N-0.041(PDBName=N,ResName=CRA,ResNum=65)         0        11.3850000000        68.4460000000       -10.8960000000 L
N-NB--0.0984(PDBName=N1,ResName=CRA,ResNum=65)     0         9.5430000000        69.5590000000        -8.4520000000 L
N-N*-0.1612(PDBName=N2,ResName=CRA,ResNum=65)      0         8.6530000000        69.9070000000       -10.5500000000 L
O-O--0.1796(PDBName=O,ResName=CRA,ResNum=65)       0         7.3260000000        71.7330000000       -10.2890000000 L
O-O-0.1144(PDBName=O1,ResName=CRA,ResNum=65)       0         6.7360000000        69.0890000000       -13.5410000000 L
O-OH-0.337(PDBName=OG,ResName=CRA,ResNum=65)       0         9.6520000000        66.3440000000        -8.4240000000 L
O-OH-0.164(PDBName=OH,ResName=CRA,ResNum=65)       0        10.3790000000        72.0870000000        -2.2450000000 L
C-CT--0.149(PDBName=CH3,ResName=NME,ResNum=68)     0         5.7564000000        67.0613000000       -11.9269000000 L
H-H-0.2719(PDBName=H,ResName=NME,ResNum=68)        0         7.7670000000        67.5708000000       -11.3969000000 L
H-H1-0.0976(PDBName=1HH3,ResName=NME,ResNum=68)    0         4.8523000000        67.6460000000       -12.0964000000 L
H-H1-0.0976(PDBName=2HH3,ResName=NME,ResNum=68)    0         6.0052000000        66.5073000000       -12.8321000000 L
H-H1-0.0976(PDBName=3HH3,ResName=NME,ResNum=68)    0         5.5878000000        66.3616000000       -11.1083000000 L
N-N--0.4157(PDBName=N,ResName=NME,ResNum=68)       0         6.8500000000        67.9490000000       -11.5870000000 L

1 2 1.0 6 2.0 34 1.5 
2 3 1.0 4 1.0 5 1.0 1 1.0 
3 2 1.0 
4 2 1.0 
5 2 1.0 
6 1 2.0 
7 10 1.0 35 1.5 36 1.5 
8 11 1.0 37 2.0 36 1.5 
9 12 1.0 38 2.0 46 1.5 
10 34 1.0 7 1.0 13 1.0 22 1.0 
11 35 1.0 8 1.0 14 2.0 
12 36 1.0 9 1.0 23 1.0 24 1.0 
13 10 1.0 39 1.0 26 1.0 27 1.0 
14 11 2.0 19 1.5 25 1.0 
15 19 1.5 17 1.5 29 1.0 
16 19 1.5 18 1.5 28 1.0 
17 15 1.5 20 1.5 31 1.0 
18 16 1.5 20 2.0 30 1.0 
19 14 1.5 15 1.5 16 1.5 
20 17 1.5 18 2.0 40 1.0 
21 34 1.0 
22 10 1.0 
23 12 1.0 
24 12 1.0 
25 14 1.0 
26 13 1.0 
27 13 1.0 
28 16 1.0 
29 15 1.0 
30 18 1.0 
31 17 1.0 
32 39 1.0 
33 40 1.0 
34 10 1.0 21 1.0 1 1.5 
35 7 1.5 11 1.0 
36 7 1.5 8 1.5 12 1.0 
37 8 2.0 
38 9 2.0 
39 13 1.0 32 1.0 
40 20 1.0 33 1.0 
41 46 1.0 43 1.0 44 1.0 45 1.0 
42 46 1.0 
43 41 1.0 
44 41 1.0 
45 41 1.0 
46 42 1.0 41 1.0 9 1.5 

NonBon 3 1 0 0  0.0000  0.0000  0.5000  0.0000  0.0000 -1.2000
HrmStr1 C  CT 313.0000  1.5240
HrmStr1 C  O  637.7000  1.2180
HrmStr1 C  N  427.6000  1.3790
HrmStr1 CT HC 330.6000  1.0970
HrmStr1 CK CT 334.8000  1.5020
HrmStr1 CK NB 441.1000  1.3690
HrmStr1 CK N* 441.1000  1.3690
HrmStr1 C  CC 371.0000  1.4680
HrmStr1 C  N* 416.9000  1.3870
HrmStr1 CT CT 300.9000  1.5380
HrmStr1 CT H1 330.6000  1.0970
HrmStr1 CT N  328.7000  1.4620
HrmStr1 CC CD 419.8000  1.4280
HrmStr1 CC NB 441.1000  1.3690
HrmStr1 CT N* 334.7000  1.4560
HrmStr1 CT OH 316.7000  1.4230
HrmStr1 CA CD 385.1000  1.4560
HrmStr1 CD HA 349.1000  1.0840
HrmStr1 CA CA 461.1000  1.3980
HrmStr1 CA HA 345.8000  1.0860
HrmStr1 CA OH 384.0000  1.3640
HrmStr1 H  N  403.2000  1.0130
HrmStr1 HO OH 371.4000  0.9730
HrmBnd1 C  CT HC 46.9000 108.7700
HrmBnd1 C  N  CT 63.4000 120.6900
HrmBnd1 C  N  H  48.3000 117.5500
HrmBnd1 CT C  O  67.4000 123.2000
HrmBnd1 CT C  N  66.8000 115.1800
HrmBnd1 HC CT HC 39.4000 107.5800
HrmBnd1 N  C  O  74.2000 123.0500
HrmBnd1 CK CT CT 63.5000 111.9300
HrmBnd1 CK CT H1 47.4000 109.6400
HrmBnd1 CK CT N  66.7000 111.7600
HrmBnd1 CC NB CK 71.0000 103.7600
HrmBnd1 C  N* CK 66.7000 120.4900
HrmBnd1 CK N* CT 67.9000 109.5100
HrmBnd1 C  CC CD 63.6000 122.6900
HrmBnd1 C  CC NB 66.2000 123.3200
HrmBnd1 C  N* CT 67.9000 109.5100
HrmBnd1 C  CT H1 47.0000 108.2200
HrmBnd1 C  CT N* 68.1000 106.5100
HrmBnd1 CT CK NB 66.0000 120.9500
HrmBnd1 CT CK N* 66.0000 120.9500
HrmBnd1 CT CT H1 46.4000 109.5600
HrmBnd1 CT CT OH 67.5000 110.1900
HrmBnd1 CT N  H  45.8000 117.6800
HrmBnd1 CC C  N* 68.6000 113.7500
HrmBnd1 CC C  O  69.1000 123.9300
HrmBnd1 CA CD CC 67.2000 111.0400
HrmBnd1 CC CD HA 47.1000 121.0700
HrmBnd1 CT CT N  65.9000 111.6100
HrmBnd1 CT OH HO 47.4000 107.2600
HrmBnd1 CD CC NB 67.6000 121.9800
HrmBnd1 CA CA CD 65.0000 120.7900
HrmBnd1 CA CA CA 66.6000 120.0200
HrmBnd1 CA CA HA 48.2000 119.8800
HrmBnd1 CA CA OH 69.5000 119.9000
HrmBnd1 CA CD HA 45.8000 124.0400
HrmBnd1 CA OH HO 49.0000 108.5800
HrmBnd1 H1 CT N  49.8000 108.8800
HrmBnd1 H1 CT H1 39.2000 108.4600
HrmBnd1 H1 CT N* 50.1000 108.5700
HrmBnd1 H1 CT OH 50.9000 110.2600
HrmBnd1 N* CK NB 69.8000 125.7000
HrmBnd1 N* C  O  73.9000 123.1800
AmbTrs CK CT N  C   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs CT CT N  C   0.530  0.000  0.150  0.500     0.0     0.0   180.0   180.0  1.0
AmbTrs H1 CT N  C   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs CT C  N  CT  1.500  0.000  0.000  0.000   180.0     0.0     0.0     0.0  1.0
AmbTrs CT C  N  H   0.000 10.000  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs O  C  CT HC  0.800  0.000  0.080  0.000     0.0     0.0   180.0     0.0  1.0
AmbTrs N  C  CT HC  0.000  0.000  0.000  0.000     0.0   180.0     0.0     0.0  6.0
AmbTrs O  C  N  CT  0.000 10.000  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs O  C  N  H   2.000  2.500  0.000  0.000     0.0   180.0     0.0     0.0  1.0
AmbTrs CK CT CT H1  0.000  0.000  1.400  0.000     0.0     0.0     0.0     0.0  9.0
AmbTrs CK CT CT OH  0.000  0.000  1.400  0.000     0.0     0.0     0.0     0.0  9.0
AmbTrs CK CT N  H   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs C  CC NB CK  0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs CD CC NB CK  0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs CC C  N* CK  0.000  8.000  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs O  C  N* CK  0.000  8.000  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs C  CT N* CK  0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs H1 CT N* CK  0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs C  CC CD CA  0.000 16.000  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs C  CC CD HA  0.000 16.000  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CT CK N* C   0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs NB CK N* C   0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs C  CT N* C   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs H1 CT N* C   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs CT CK NB CC  0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs CT CK N* CT  0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs CT CT OH HO  0.250  0.000  0.160  0.000     0.0     0.0     0.0     0.0  1.0
AmbTrs CC C  N* CT  0.000  8.000  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs CA CA CD CC  0.000  2.800  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs N* CK NB CC  0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs NB CK N* CT  0.000  9.500  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs O  C  N* CT  0.000  8.000  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs NB CK CT CT  0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs N* CK CT CT  0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs CT CT N  H   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs N* C  CC CD  0.000 11.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs O  C  CC CD  0.000 11.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CA CA CA CD  0.000 14.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CD CA CA HA  0.000 14.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CA CA CA CA  0.000 14.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CA CA CA OH  0.000 14.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CA CA CD HA  0.000  2.800  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CA CA CA HA  0.000 14.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs CA CA OH HO  0.000  1.800  0.000  0.000     0.0   180.0     0.0     0.0  2.0
AmbTrs NB CC CD CA  0.000 16.000  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs H1 CT N  H   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs NB CK CT H1  0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs N* CK CT H1  0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs H1 CT CT H1  0.000  0.000  1.400  0.000     0.0     0.0     0.0     0.0  9.0
AmbTrs H1 CT CT OH  0.250  0.000  0.000  0.000     0.0     0.0     0.0     0.0  1.0
AmbTrs O  C  CT H1  0.800  0.000  0.080  0.000     0.0     0.0   180.0     0.0  1.0
AmbTrs N  C  CT H1  0.000  0.000  0.000  0.000     0.0   180.0     0.0     0.0  6.0
AmbTrs NB CC CD HA  0.000 16.000  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs H1 CT CT N   0.000  0.000  1.400  0.000     0.0     0.0     0.0     0.0  9.0
AmbTrs H1 CT OH HO  0.000  0.000  0.500  0.000     0.0     0.0     0.0     0.0  3.0
AmbTrs HA CA CA HA  0.000 14.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs HA CA CA OH  0.000 14.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs NB CK CT N   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs N* CK CT N   0.000  0.000  0.000  0.000     0.0     0.0     0.0     0.0  6.0
AmbTrs N  CT CT OH  0.000  0.000  1.400  0.000     0.0     0.0     0.0     0.0  9.0
AmbTrs N* C  CC NB  0.000 11.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs O  C  CC NB  0.000 11.500  0.000  0.000     0.0   180.0     0.0     0.0  4.0
AmbTrs O  C  CT N*  0.000  0.000  0.000  0.000     0.0   180.0     0.0     0.0  6.0
AmbTrs N  C  CT N*  0.000  0.000  0.000  0.000     0.0   180.0     0.0     0.0  6.0
ImpTrs CT N  C  O   1.1000  180.0  2.0
ImpTrs CT N* CK NB 10.5000  180.0  2.0
ImpTrs CC N* C  O   1.1000  180.0  2.0
ImpTrs C  C  CC NB  1.1000  180.0  2.0
ImpTrs CA CC CD CC  1.1000  180.0  2.0
ImpTrs CA CA CA HA  1.1000  180.0  2.0
ImpTrs CA CA CA CA  1.1000  180.0  2.0
ImpTrs CA CA CA CD  1.1000  180.0  2.0
ImpTrs C  CT N  H   1.1000  180.0  2.0
ImpTrs C  CK N* CT  1.1000  180.0  2.0
VDW C   1.9080  0.0860
VDW CT  1.9080  0.1094
VDW HC  1.4870  0.0157
VDW O   1.6612  0.2100
VDW CK  1.9080  0.0860
VDW CC  1.9080  0.0860
VDW CD  1.9080  0.0860
VDW CA  1.9080  0.0860
VDW H   0.6000  0.0157
VDW H1  1.3870  0.0157
VDW HA  1.4590  0.0150
VDW HO  0.0000  0.0000
VDW N   1.8240  0.1700
VDW NB  1.8240  0.1700
VDW N*  1.8240  0.1700
VDW OH  1.7210  0.2104

DW     0.7210  0.2104

  0.0000  0.0
AmbTrs O  C  CT N*    0.0    0.0    0.0    0.0  0.0000 180.0000  0.0000  0.0000  0.0
AmbTrs N  C  CT N*    0.0    0.0    0.0    0.0  0.0000 180.0000  0.0000  0.0000  0.0
ImpTrs CT N  C  O   1.1000  180.0  2.0
ImpTrs CT N* CK NB 10.5000  180.0  2.0
ImpTrs CC N* C  O   1.1000  180.0  2.0
ImpTrs C  C  CC NB  1.1000  180.0  2.0
ImpTrs CA CC CD CC  1.1000  180.0  2.0
ImpTrs CA CA CA HA  1.1000  180.0  2.0
ImpTrs CA CA CA CA  1.1000  180.0  2.0
ImpTrs CA CA CA CD  1.1000  180.0  2.0
ImpTrs C  CT N  H   1.1000  180.0  2.0
ImpTrs C  CK N* CT  1.1000  180.0  2.0
VDW C   0.0000  0.0000
VDW CT  0.0000  0.0000
VDW HC  0.0000  0.0000
VDW O   0.0000  0.0000
VDW CK  0.0000  0.0000
VDW CC  0.0000  0.0000
VDW CD  0.0000  0.0000
VDW CA  0.0000  0.0000
VDW H   0.0000  0.0000
VDW H1  0.0000  0.0000
VDW HA  0.0000  0.0000
VDW HO  0.0000  0.0000
VDW N   0.0000  0.0000
VDW NB  0.0000  0.0000
VDW N*  0.0000  0.0000
VDW OH  0.0000  0.0000
VDW     0.7210  0.2104

