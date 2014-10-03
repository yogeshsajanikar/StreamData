
# FileBlob : A custom data type for NHibernate to handle files on disk

A file blob is a custom data type for using files on the disk as a
data type. This data type spans two columns:

* **Blob Column**: This column contains the binary blob data.
* **Blob Information Column**: This column contains the information
  about the file. This includes 
  * File Name, and
  * File Hash.
  The format in which the information is written to the information column is
        File Name;File Hash (HEX String)


