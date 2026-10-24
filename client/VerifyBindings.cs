using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class VerifyBindings
{
    private static string root;
    private static string output;
    private const BindingFlags All=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    public static int Main(string[] args)
    {
        try
        {
            root=args[0];output=args[1];
            AppDomain.CurrentDomain.AssemblyResolve+=Resolve;
            var assembly=Assembly.LoadFrom(Path.Combine(output,"HardcoreProgression.Client.dll"));
            int count=0;
            foreach(string name in new[]{"FullDescriptionPatch","ShortDescriptionPatch"})
            {
                var type=assembly.GetType("HardcoreProgression.Client."+name,true);
                var target=(MethodBase)type.GetMethod("GetTargetMethod",All).Invoke(RuntimeHelpers.GetUninitializedObject(type),null);
                if(target==null || target.ContainsGenericParameters)throw new Exception("Unbound target: "+name);
                var patch=type.GetMethod("Postfix",All);
                foreach(var p in patch.GetParameters())
                {
                    Type expected=p.Name=="__instance"?target.DeclaringType:target.GetParameters().Single(x=>x.Name==p.Name).ParameterType;
                    if(!p.ParameterType.IsAssignableFrom(expected))throw new Exception("Wrong patch parameter "+name+"."+p.Name);
                }
                var field=target.DeclaringType.GetField("_description",All);
                if(field==null)throw new Exception("Description field missing");
                Type fieldType=field.FieldType;
                while(fieldType!=null && fieldType.FullName!="TMPro.TMP_Text")fieldType=fieldType.BaseType;
                if(fieldType==null)throw new Exception("Description field is not TMP text");
                Console.WriteLine("PASS client binding: "+target.DeclaringType.FullName+"."+target.Name);
                count++;
            }
            return count==2?0:1;
        }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static Assembly Resolve(object sender,ResolveEventArgs args)
    {
        string file=new AssemblyName(args.Name).Name+".dll";
        foreach(string dir in new[]{output,Path.Combine(root,"EscapeFromTarkov_Data","Managed"),Path.Combine(root,"BepInEx","core"),Path.Combine(root,"BepInEx","plugins","spt")})
        {
            string path=Path.Combine(dir,file);
            if(File.Exists(path))return Assembly.LoadFrom(path);
        }
        return null;
    }
}
