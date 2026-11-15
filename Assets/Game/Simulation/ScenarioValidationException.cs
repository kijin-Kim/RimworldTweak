using System;

namespace RimworldTweak.Simulation
{
    public sealed class ScenarioValidationException : ArgumentException
    {
        public ScenarioValidationException(string code, string message)
            : base(message, "scenario")
        {
            Code = code;
        }

        public string Code { get; }
    }
}
