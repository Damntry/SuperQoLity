using Damntry.Utils;
using System;
using System.Reflection;

namespace SuperQoLity {
    public static class AssemblyResolver {

        public static void Init() {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEventHandler;
        }

        private static Assembly ResolveEventHandler(object sender, ResolveEventArgs args) {
            if (args.Name == "Microsoft.Bcl.HashCode") {
                return EmbeddedReferenceResolve.LoadEmbeddedResource(Properties.Resources.Microsoft_Bcl_HashCode);
            }

            return null;
        }

    }
}
