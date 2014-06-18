/**
 * 
 * 
 * 
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using MasterProgram.Dmp.Collections;
using Dmp.Neptune.Collections;
using DMP.MasterProgram.Utils.Caching;
using DMP.MasterProgram.Utils;

namespace DMP.MasterProgram.Processors
{
    public abstract class IRecordProcessor : IDisposable
    {
         public bool isOutputProcessor =false;
         public abstract AbstractRecord ProcessRecord(AbstractRecord record);
         public abstract void InitializeMetaData(string xmlString);
         public abstract void SetImpactorCacheMap(Dictionary<string, DataSet> cache);
         public abstract void SetImpactors(Dictionary<string, List<AbstractRecord>> impactors);


         #region IDisposable Members

         public abstract void Dispose();
         
         #endregion
    }
}
