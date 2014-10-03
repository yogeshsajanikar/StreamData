﻿using System;
using System.Text;
using System.Diagnostics;
using System.Data;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using NHibernate.UserTypes;

namespace NHibernate.UserTypes
{
	/// <summary>
	/// File blob represents a file on the disk. A file blob holds information 
	/// about file (path).
	/// </summary>
	public class FileBlob
	{
		/// <summary>
		/// Gets the path to the file
		/// </summary>
		public virtual string Path 
		{ 
			get {
				return _info.FullName;
			}
		}

		/// <summary>
		/// Gets the name of the file.
		/// </summary>
		public virtual string Name 
		{
			get {
				return _info.Name;
			}
		}

		/// <summary>
		/// Create a file blob from the given path.
		/// </summary>
		/// <exception cref="FileNotFoundException">
		/// If the given path does not point to a valid file, the exception is 
		/// raised.
		/// </exception>
		/// <param name="path">Path to the file.</param>
		public FileBlob (string path)
		{
			if (!File.Exists (path))
				throw new FileNotFoundException ("File " + path + " does not exist");
			  
			_info = new FileInfo (path);
		}

		private FileInfo _info;
	}

	public class LocationProvider
	{
		/// <summary>
		/// Gets the location, where the file blob should be stored. This behaviour 
		/// can be overridden in the derived class. The location provider needs to be
		/// registered in the <cref="NHibernate.UserTypes.LocationRegister"/>.
		/// 
		/// The function is passed SHA1 hash of the file stored in the database. 
		/// Implementation can use the hash to decide whether the file needs to be 
		/// downloaded.
		/// </summary>
		/// <returns>The location where the FileBlob will be saved. It also 
		/// returns a boolean that tells NHibernate whether to download the file 
		/// or not. The default returns false, if a file with same name exists
		/// at the given location, i.e. it does not try to match the hash.
		/// </returns>
		/// <param name="location">Location parameter set in the class map</param>
		/// <param name="name">Name of the file, stored in the database.</param>
		/// <param name="hash">Hash of the file </param>
		public virtual Tuple<bool, string> GetLocation(string location, string name, byte [] hash)
		{
			if (string.IsNullOrEmpty (location))
				location = _defaultLocation.FullName;

			var filename = Path.Combine (location, name);
			if (File.Exists (filename))
				return System.Tuple.Create (false, filename);

			return System.Tuple.Create(true, filename);
		}

		static LocationProvider()
		{
			var local = Environment.GetFolderPath (
				Environment.SpecialFolder.LocalApplicationData, 
				Environment.SpecialFolderOption.Create);

			var localData = Path.Combine (local, "FileBlobData");

			if (!Directory.Exists (localData))
				Directory.CreateDirectory (localData);

			_defaultLocation = new DirectoryInfo (localData);
		}

		static DirectoryInfo _defaultLocation;
	};

	public static class LocationRegister 
	{

		public static LocationProvider GetLocationProvider(string location)
		{
			LocationProvider provider = _defaultLocationProvider;
			if (_locationRegister.TryGetValue (location, out provider))
				return provider;

			return _defaultLocationProvider;
		}

		static LocationRegister ()
		{
			_locationRegister = new Dictionary<string, LocationProvider> ();
			_defaultLocationProvider = new LocationProvider ();
		}

		static LocationProvider _defaultLocationProvider;
		static Dictionary<string, LocationProvider> _locationRegister;
	}

	internal class BlobFileInfo 
	{
		public string Name { get; set; }

		public byte [] Hash { get; set; }

		public override string ToString ()
		{
			var sbuilder = new StringBuilder ();
			foreach (var b in Hash)
				sbuilder.Append (b.ToString ("x2"));

			return string.Format ("{0};{1}", Name, Hash);
		}

		public static BlobFileInfo FromString(string infoString)
		{
			char [] separators = { ';' };
			string [] info = infoString.Split(separators);
			if (info.Length != 2)
				new InvalidDataException("Invalid file, hash information in the blob");

			var hash = Enumerable.Range(0, info[1].Length)
				.Where(i => 0 == i % 2)
				.Select(x => Convert.ToByte(info[1].Substring(x, 2), 16))
				.ToArray();

			return new BlobFileInfo () { Hash = hash, Name = info[0] };
		}
	}

	/// <summary>
	/// Represents a user type <cref="NHibernate.UserTypes.FileBlob"/>. The file
	/// is uploaded to database from the location specified by user. While getting the 
	/// file back from the database, the file is stored at the location specified
	/// by the parameter "location". If the location is not specified, the file is 
	/// stored  
	/// </summary>
	public class FileBlobType : IUserType, IParameterizedType
	{
		/// <summary>
		/// Return the hash code of the file blob. The contents are not checked, only path.
		/// </summary>
		/// <returns>The hash code.</returns>
		/// <param name="x">
		/// The object for which hash needs to be found. In this case, a 
		/// <cref="NHibernate.UserTypes.FileBlob"/>
		/// </param>
		public int GetHashCode (object x)
		{
			return x.GetHashCode ();
		}

		/// <summary>
		/// First get the information about the name of the file, and then pass the information
		/// to location provider. The location provider supplies the path where the blob needs 
		/// to be saved. The location provider also informs whether blob needs to be fetched. 
		/// <cref="NHibernate.UserTypes.LocationProvider"/>
		/// 
		/// The file is then read sequentially, with 4k bytes in each step. Till all the bytes
		/// are written to the file. 
		/// </summary>
		/// <param name="rs">a IDataReader</param>
		/// <param name="names">column names</param>
		/// <param name="owner">the containing entity</param>
		/// <returns></returns>
		/// <exception cref="T:NHibernate.HibernateException">HibernateException</exception>
		public object NullSafeGet (IDataReader rs, string[] names, object owner)
		{
			try {
				// Get the ordinals for the given columns. Note that we are assuming that
				// the column names are in the sequence in which sqltypes are sequenced.
				int blobIndex = rs.GetOrdinal(names[0]);
				int hashIndex = rs.GetOrdinal(names[1]);

				// Get the location provider
				var provider = LocationRegister.GetLocationProvider(this._location);
				var hashInfo = rs.GetString(hashIndex);

				var blobInfo = BlobFileInfo.FromString(hashInfo);
				var fileinfo = provider.GetLocation(_location, blobInfo.Name, blobInfo.Hash);

				if (!fileinfo.Item1)
					return new FileBlob(fileinfo.Item2); // No need to get the file.

				// Read the file. Save it to the given location
				using (var bw = new FileStream(fileinfo.Item2, FileMode.Create))
				{
					const int bufsize = 0x1000;
					byte [] buffer = new byte[bufsize];
					long cursor = 0, bytesRead = 0;
					while ((bytesRead = rs.GetBytes(blobIndex, cursor, buffer, 0, bufsize)) > 0)
					{
						cursor += bytesRead;
						bw.Write(buffer, 0, (int)bytesRead);

						if (bytesRead < bufsize)
							break;
					}
				}

				return new FileBlob(fileinfo.Item2);
			}
			catch (Exception exc) {
				//TODO: Add logging support
				Debug.Assert (false, exc.Message);
			}

			return null;
		}

		/// <summary>
		/// All the contents of the file are loaded into bytearray and passed to the db command
		/// alongwith SHA1 hash and the name of the file to be saved in the second column. 
		/// The large file should consume large amount of memory, but it should be temporary as
		/// the this happens only during sql query execution.
		/// </summary>
		/// <param name="cmd">a database command being configured</param>
		/// <param name="value">
		/// The object to write, <cref="NHibernate.UserTypes.FileBlob"/> in this case.
		/// </param>
		/// <param name="index">
		/// The index of the parameter. Note that we fill up both the columns in one go. 
		/// </param>
		/// <exception cref="T:NHibernate.HibernateException">HibernateException</exception>
		public void NullSafeSet (IDbCommand cmd, object value, int index)
		{
			var dbparam = cmd.Parameters [index] as IDbDataParameter;
			if (dbparam.DbType != DbType.Binary)
				return;

			Debug.Assert (value is FileBlob);

			var blob = value as FileBlob;
			byte[] data = File.ReadAllBytes (blob.Path);
			dbparam.Value = data;

			var hasher = SHA1.Create ();
			var hash   = hasher.ComputeHash (data);

			var infoparam = cmd.Parameters [index + 1] as IDbDataParameter;
			Debug.Assert (infoparam.DbType == DbType.String);

			infoparam.Value = new BlobFileInfo () { Name = blob.Name, Hash = hash }.ToString ();
		}

		/// <summary>
		/// Just return the object itself.
		/// </summary>
		/// <param name="value">an object being copied, FileBlob in thie case.</param>
		/// <returns>a copy</returns>
		public object DeepCopy (object value)
		{
			return value;
		}

		/// <summary>
		/// This is an immutable object, just return the original object.
		/// </summary>
		/// <param name="original">Original.</param>
		/// <param name="target">Target.</param>
		/// <param name="owner">Owner.</param>
		public object Replace (object original, object target, object owner)
		{
			return original;
		}

		/// <summary>
		/// Do not do anything special, just return the cached object.
		/// </summary>
		/// <param name="cached">the object to be cached</param>
		/// <param name="owner">the owner of the cached object</param>
		/// <returns>a reconstructed object from the cachable representation</returns>
		public object Assemble (object cached, object owner)
		{
			return DeepCopy (cached);
		}

		/// <summary>
		/// Simply returns the object itself.
		/// </summary>
		/// <param name="value">the object to be cached</param>
		/// <returns>a cacheable representation of the object</returns>
		public object Disassemble (object value)
		{
			return DeepCopy (value);
		}


		/// <summary>
		/// The SQL types for the columns mapped by this type. The blob
		/// is comprised of two columns, first column stores the blob,
		/// the second stores the name, along with hash of the file.
		/// </summary>
		/// <value>The sql types.</value>
		public NHibernate.SqlTypes.SqlType[] SqlTypes {
			get {
				NHibernate.SqlTypes.SqlType[] sqlTypes = {
					new NHibernate.SqlTypes.BinaryBlobSqlType (),
					new NHibernate.SqlTypes.StringSqlType ()
				};

				return sqlTypes;
			}
		}

		/// <summary>
		/// Retruns type of FileBlob.
		/// </summary>
		/// <value>The type of the returned.</value>
		public System.Type ReturnedType {
			get {
				return typeof(FileBlob);
			}
		}

		/// <summary>
		/// File blob cannot be merged. It is immutable.
		/// </summary>
		/// <value><c>true</c> Always return false</value>
		public bool IsMutable {
			get {
				return false;
			}
		}

		#region IParameterizedType implementation
		public void SetParameterValues (System.Collections.Generic.IDictionary<string, string> parameters)
		{
			string value;
			if (parameters.TryGetValue (_locationKey, out value))
				_location = value;
		}

		#endregion

		public FileBlobType()
		{

		}

		private string _location = string.Empty;
		private const string _locationKey = "location";
	}
}
