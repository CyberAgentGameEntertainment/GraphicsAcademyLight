#if UNITY_EDITOR

using System.Collections.Generic;

namespace Sirius.PostProcessing
{
    public interface IUsingShaderProvider
    {
        IEnumerable<string> GetUsingShaderNameList();
    }
}

#endif
