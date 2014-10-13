
# StreamData : A custom data type for NHibernate to handle Streams on disk

## Need
When working with project where many files need to be stored in the database,
using byte array adds to the memory consumption. This project implements a
simple custom type that converts a file into byte array, only when it is
required (i.e. while committing to the database). 

## Design
A stream data is a custom data type for using streams (like files on
the disk) as a data type. This data type spans two columns: 

* **Blob Column**: This column contains the binary blob data.
* **Blob Information Column**: This column contains the information
  about the file. This includes 
  * File Name, and
  * File Hash.

  The format in which the information is written to the information column is

        File Name;File Hash (HEX String)

## Sample Code

