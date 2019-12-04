FN=$(dirname "$0")"/Arrays"
gfortran -O2 -g -fPIC -c -o "$FN".o "$FN".f90
gfortran -O2 -g -fPIC -dynamiclib "$FN".o -o "$FN".dll
gfortran -O2 -g -fPIC -shared "$FN".o -o "$FN".bundle
