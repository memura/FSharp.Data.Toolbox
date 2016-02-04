Obtain the latest source for HDF5 ... the one used was http://www.hdfgroup.org/ftp/HDF5/current/src/hdf5-1.8.16.zip
Get the appropriate version of Cmake and install it ... the one used was https://cmake.org/files/v3.5/cmake-3.5.0-rc1-win32-x86.msi

configure and generate the HDF5 project
copy from the cmake build directory the file H5pubconf.h and put it into the src directory that contains all the H*public.h files

open a Developer Command Prompt and move to the source directory and give the preprocessing command

cl.exe /P *public.h

and this creates a set of files *public.i that we then need to parse using the custom parsing code

