using System;

namespace BlogApplication.Controllers
{
    public class TrimController
    {
		delegate byte PerformCalculation(int x);
		static public byte eventhandler (int x)
		{
			if (!x.Equals(1))
			{
				throw new Exception("Given controller name does not contain the word Controller.");
			}



			return ((byte)x);
		}

		public void f()
		{
			PerformCalculation f = eventhandler;

			f(1);
		}


	}
}
