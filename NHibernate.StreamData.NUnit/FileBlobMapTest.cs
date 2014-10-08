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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using NHibernate.UserTypes;
using FluentNHibernate.Mapping;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using NHibernate.Tool.hbm2ddl;
using System.IO;

namespace NHibernate.FileBlob.NUnit
{
    class BlobData
    {
        public virtual int Id { get; set; }
        public virtual IStreamProvider Provider { get; set; }
    }

    class BlobDataMap : ClassMap<BlobData>
    {
        public BlobDataMap()
        {
            Id(x => x.Id);
            Map(x => x.Provider, "STREAM_DATA")
                .Columns.Add("STREAM_INFO")
                .CustomType<StreamProviderType>();
        }
    }

    [TestFixture]
    public class FileBlobMapTest
    {

        [SetUp]
        public void SetUp()
        {
            if (!Directory.Exists("Test"))
                Directory.CreateDirectory("Test");

            if (File.Exists(@"Test\RandomBytes"))
                File.Delete(@"Test\RandomBytes");

            File.Copy("RandomBytes", @"Test\RandomBytes");
            _data = File.ReadAllBytes("RandomBytes");
        }

        private ISessionFactory CreateFactory()
        {
            var factory = Fluently.Configure()
                .Database(SQLiteConfiguration.Standard.UsingFile(_dbFile))
                .Mappings(m => m.FluentMappings.Add<BlobDataMap>())
                .ExposeConfiguration(config =>
                {
                    if (File.Exists(_dbFile))
                        File.Delete(_dbFile);

                    new SchemaExport(config).Create(true, true);
                })
                .BuildSessionFactory();

            return factory;
        }

        [TearDown]
        public void TearDown()
        {
        }

        [Test]
        public void Test()
        {
            using (var factory = CreateFactory())
            {
                int id = Populate(factory);

                using (var session = factory.OpenSession())
                using (var transaction = session.BeginTransaction())
                {
                    var blobdata = session.Get<BlobData>(id);
                    Assert.IsNotNull(blobdata, "Blob not found");
                    var provider = blobdata.Provider;
                    var datainfo = provider.ReadAllBytes();

                    Assert.IsTrue(Enumerable.SequenceEqual(datainfo.Item1, _data), "Data Corruption");
                };
            }


        }

        private int Populate(ISessionFactory factory)
        {
            using (var session = factory.OpenSession())
            using (var transaction = session.BeginTransaction())
            {
                var blobdata = new BlobData { Provider = new FileStreamProvider("RandomBytes") };
                session.Save(blobdata);
                transaction.Commit();
                return blobdata.Id;
            }
        }


        byte[] _data = null;
        string _dbFile = "Blob.db";
    }
}

