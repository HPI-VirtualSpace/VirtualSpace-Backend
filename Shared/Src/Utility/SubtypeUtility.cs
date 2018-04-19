using System;

namespace VirtualSpace.Shared
{
    public static class SubtypeUtility
    {
        public static T ConvertToSubtype<S, T>(T derived)
            where T : S
        {
            return derived;
        }

        public static Type GetTypeOfSubtype<S>(S super)
        {
            return ConvertToSubtype<S, S>(super).GetType();
        }
    }
}