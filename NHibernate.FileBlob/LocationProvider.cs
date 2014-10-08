/**
 * The MIT License (MIT)
 * 
 * Copyright (c) 2014 Yogesh Sajanikar (yogesh_sajanikar@yahoo.com)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 **/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NHibernate.UserTypes.Utils
{

    /// <summary>
    /// A location provider helps class map dynamically map the location parameter, 
    /// and file name to a valid path name on the drive. 
    /// </summary>
    public class LocationProvider
    {
		public virtual IStreamProvider GetLocation( string location, string name)
		{
			if (string.IsNullOrEmpty(location))
				location = _defaultLocation.FullName;

			var filename = Path.Combine(location, name);
			return new FileStreamProvider (filename);
		}

        /// <summary>
        /// Resolves the location, where the file blob should be stored. This behaviour 
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
        public virtual Tuple<bool, IStreamProvider> ResolveLocation (string location, string name, byte[] hash)
        {
			var streamProvider = GetLocation (location, name);

			if (File.Exists(streamProvider.Provenance))
                return System.Tuple.Create(false, streamProvider);

            return System.Tuple.Create(true, streamProvider);
        }

        /// <summary>
        /// Initializes default locations. By default the files are retrieved in
        /// "FileBlobData" subfolder in <see cref="System.Environment.SpecialFolder.LocalApplicationData">
        /// Local Application Data Folder</see>. 
        /// </summary>
        static LocationProvider()
        {
            var local = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create);

            var localData = Path.Combine(local, "FileBlobData");

            if (!Directory.Exists(localData))
                Directory.CreateDirectory(localData);

            _defaultLocation = new DirectoryInfo(localData);
        }

        static DirectoryInfo _defaultLocation;
    };

    /// <summary>
    /// Location Register provides LocationProvider service for a given
    /// location parameter, being registered with the class map.
    /// </summary>
    public static class LocationRegister
    {

        /// <summary>
        /// Retrieves location provider for a given location. If no location provider
        /// is registered with the register, it returns default location provider.
        /// </summary>
        /// <param name="location">Location parameter registered with class map</param>
        /// <returns>Returns location provider associated with given location</returns>
        public static LocationProvider GetLocationProvider(string location)
        {
            LocationProvider provider = _defaultLocationProvider;
            if (_locationRegister.TryGetValue(location, out provider))
                return provider;

            return _defaultLocationProvider;
        }

        /// <summary>
        /// Initializes registers
        /// </summary>
        static LocationRegister()
        {
            _locationRegister = new Dictionary<string, LocationProvider>();
            _defaultLocationProvider = new LocationProvider();
        }

        static LocationProvider _defaultLocationProvider;
        static Dictionary<string, LocationProvider> _locationRegister;
    }
}
