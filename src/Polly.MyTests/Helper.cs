using System;
using System.Text;

namespace Sandbox.Polly
{
    public class Helper
    {
        public static void Throw<TException>() 
            where TException: Exception, new()
        {
            throw new TException();
        }
        
      


        public static T In<T>(T action)
            where T : Delegate
        {
            return action;
        }
        
    }
}