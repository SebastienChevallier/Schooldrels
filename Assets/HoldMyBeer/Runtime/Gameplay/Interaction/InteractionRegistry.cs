using System;
using System.Collections.Generic;
using HoldMyBeer.Interaction;

namespace HoldMyBeer.Gameplay.Interaction
{
    /// <summary>
    /// Walks the rules in registration order and returns the first that accepts.
    /// Deliberately the same shape as ConnectionApprovalHandler: one list, first match
    /// wins, no scoring and no priority field to get wrong.
    /// </summary>
    public sealed class InteractionRegistry : IInteractionRegistry
    {
        private readonly List<IInteractionRule> _rules = new();

        public void AddRule(IInteractionRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            _rules.Add(rule);
        }

        public bool TryResolve(in InteractionRequest request, out IInteractionRule rule, out string prompt)
        {
            // Indexed rather than foreach: this runs every frame on the owner to keep
            // the crosshair prompt current, and the enumerator would allocate.
            for (var i = 0; i < _rules.Count; i++)
            {
                if (_rules[i].CanApply(in request, out prompt))
                {
                    rule = _rules[i];
                    return true;
                }
            }

            rule = null;
            prompt = null;
            return false;
        }
    }
}
