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

using System.IO;
using System.Security.Cryptography;

namespace NHibernate.UserTypes
{
    /// <summary>
    /// Provides a stream from where file can be read or written.
    /// </summary>
    public interface IStreamProvider
    {
        /// <summary>
        /// Gets the provenance of the stream. In case of a file, it can be
        /// a plain path. In other cases, this depends upon the implementation
        /// where it is possible to resolve the provenance to the source. For
        /// example, a URI can be provided which can be parsed by application
        /// to download the file to local disk before creating the stream.
        /// 
        /// Note that provenance is created by resolving name, and location of 
        /// the location provider.
        /// </summary>
        /// <value>The path.</value>
        string Provenance { get; }

        /// <summary>
        /// A string that identities the stream. The name is stored in the 
        /// database, in the information column alongwith its hash. In case
        /// of a plain file, the name can be file name, where as in other cases
        /// it can point to something that uniquely identifies the resource
        /// in its location as pointed by location provider.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Get the stream from which information can be read.
        /// </summary>
        Stream Reader { get; }

        /// <summary>
        /// Gets the writer stream where file can be written
        /// </summary>
        Stream Writer { get; }
    }

    /// <summary>
    /// Extension class for <see cref="NHibernate.UserTypes.IStreamProvider"/>
    /// </summary>
    public static class StreamProviderExtension
    {
        /// <summary>
        /// Read all the bytes from the given stream. 
        /// </summary>
        /// <param name="provider"></param>
        /// <returns></returns>
        public static System.Tuple<byte[],byte[]> ReadAllBytes(this IStreamProvider provider)
        {
            using (Stream rStream = provider.Reader)
            {
                var hasher = SHA1.Create();

                byte[] bytes = new byte[rStream.Length];
                const int BUFSIZE = 0x1000;

                int bufsize = BUFSIZE < (int)rStream.Length ? BUFSIZE : (int)rStream.Length;

                int cursor = 0;
                int bytesRead = 0;

                while ((bytesRead = rStream.Read(bytes, cursor, bufsize)) > 0)
                {
                    cursor += bytesRead;
                    hasher.ComputeHash(bytes, 0, bytesRead);

                    if (bytesRead < bufsize || cursor >= bytes.Length)
                        break;
                }

                return System.Tuple.Create(bytes, hasher.Hash);
            }
        }
    }


    /// <summary>
    /// Provides the file based stream provider. This is also a default stream
    /// provider for the location provider. 
    /// </summary>
    public class FileStreamProvider : IStreamProvider
    {
        #region IStreamProvider implementation
        public string Provenance
        {
            get
            {
                return _fileInfo.FullName;
            }
        }

        public string Name
        {
            get
            {
                return _fileInfo.Name;
            }
        }
        public Stream Reader
        {
            get
            {
                return _fileInfo.OpenRead();
            }
        }
        public Stream Writer
        {
            get
            {
                return _fileInfo.OpenWrite();
            }
        }
        #endregion

        public FileStreamProvider(string path)
        {
            _fileInfo = new FileInfo(path);
        }

        FileInfo _fileInfo;
    }

    /// <summary>
    /// BinaryDataProvider represents a fixed size binary data (non-resizable).
    /// </summary>
    public class BinaryDataProvider : IStreamProvider
    {

        public string Provenance
        {
            get { return _name; }
        }

        public string Name
        {
            get { return _name; }
        }

        public Stream Reader
        {
            get { return new MemoryStream(_data); }
        }

        public Stream Writer
        {
            get { return new MemoryStream(_data); }
        }

        public BinaryDataProvider(string name, byte[] data)
        {
            _name = name;
            _data = data;
        }

        string _name;
        byte[] _data;
    }

}
