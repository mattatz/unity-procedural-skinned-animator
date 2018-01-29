
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSkinAnim {

	public class Kernel
	{
		public int Index { get { return index; } }
		public int ThreadX { get { return (int)threadX; } }
		public int ThreadY { get { return (int)threadY; } }
		public int ThreadZ { get { return (int)threadZ; } }

		int index;
		uint threadX, threadY, threadZ;

		public Kernel(ComputeShader shader, string key)
		{
			index = shader.FindKernel(key);
			if (index < 0)
			{
				Debug.LogWarning("Can't find kernel");
				return;
			}
			shader.GetKernelThreadGroupSizes(index, out threadX, out threadY, out threadZ);
		}
	}

}
