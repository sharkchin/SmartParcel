using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.Common;

namespace DMP.MasterProgram.ProcessorMetadata
{
    public class OutputProcessorMetadata
    {
        private string storageType;
        private string tableName;
        private string database;
        private Dictionary<string, string> attributes;
        private string[] indexedFields;

        public OutputProcessorMetadata()
        {
            this.attributes = new Dictionary<string, string>();
        }

        public String DataPath { get; set; }

        public System.Xml.Linq.XElement LayerMetadataXml { get; set; }

        public string StorageType
        {
            get
            {
                return this.storageType;
            }
            set
            {
                this.storageType = value;
            }
        }

        public string TableName
        {
            get
            {
                return this.tableName;
            }
            set
            {
                this.tableName = value;
            }
        }

        public string Database
        {
            get
            {
                return this.database;
            }

            set
            {
                this.database = value;
            }
        }

        public Dictionary<string, string> Attributes
        {
            get
            {
                return this.attributes;
            }
        }

        public string[] IndexedFields
        {
            get
            {
                return this.indexedFields;
            }
            set
            {
                this.indexedFields = value;
            }
        }
        
        
         
        
    }
}
