using System;
using System.Collections.Generic;
using System.Linq;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Diagnostics;
using PilotBim.Analytics.Models;

namespace PilotBim.Analytics.Discovery
{
    internal sealed class StateDiscoveryService
    {
        private readonly IObjectsRepository _repository;

        public StateDiscoveryService(IObjectsRepository repository)
        {
            _repository = repository;
        }

        public List<StateInventoryRecord> DiscoverStates()
        {
            var list = new List<StateInventoryRecord>();
            try
            {
                foreach (var state in ObjectRepositoryExtensions.GetUserStates(_repository))
                {
                    if (state == null)
                        continue;
                    list.Add(new StateInventoryRecord
                    {
                        StateId = state.Id,
                        Name = state.Name,
                        Title = string.IsNullOrWhiteSpace(state.Title) ? state.Name : state.Title,
                        Color = state.Color.ToString(),
                        IsDeleted = state.IsDeleted,
                        SemanticStatus = CapabilityStatus.Unknown
                    });
                }

                InferSemantics(list);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("state-discovery", ex);
                throw;
            }

            return list.OrderBy(s => s.Title).ToList();
        }

        public void ApplyObservedUsage(List<StateInventoryRecord> states, IEnumerable<TypeInventoryRecord> types)
        {
            if (states == null || types == null)
                return;

            var map = states.ToDictionary(s => s.StateId, s => s);
            foreach (var type in types)
            {
                if (type.ObservedStateCounts == null)
                    continue;
                foreach (var pair in type.ObservedStateCounts)
                {
                    StateInventoryRecord state;
                    if (!map.TryGetValue(pair.Key, out state))
                        continue;
                    state.ReferencedObjects += pair.Value;
                    if (!state.ReferencedTypes.Contains(type.Title ?? type.Name))
                        state.ReferencedTypes.Add(type.Title ?? type.Name);
                }
            }
        }

        public static void InferSemantics(List<StateInventoryRecord> states)
        {
            if (states == null)
                return;

            foreach (var state in states)
            {
                var text = ((state.Title ?? state.Name) ?? string.Empty).ToLowerInvariant();
                if (ContainsAny(text, "закры", "closed", "resolved", "done", "complete", "выполн"))
                    state.SemanticStatus = "CLOSED";
                else if (ContainsAny(text, "откры", "open", "new", "нов", "актив", "active", "в работе"))
                    state.SemanticStatus = "OPEN";
                else if (ContainsAny(text, "отклон", "reject", "cancel"))
                    state.SemanticStatus = "REJECTED";
                else
                    state.SemanticStatus = CapabilityStatus.Unknown;
            }
        }

        private static bool ContainsAny(string text, params string[] parts)
        {
            foreach (var part in parts)
            {
                if (text.IndexOf(part, StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }
    }

    internal sealed class PersonDiscoveryService
    {
        private readonly IObjectsRepository _repository;

        public PersonDiscoveryService(IObjectsRepository repository)
        {
            _repository = repository;
        }

        public List<PersonInventoryRecord> DiscoverPersons(IDictionary<int, OrganisationInventoryRecord> orgs)
        {
            var list = new List<PersonInventoryRecord>();
            try
            {
                foreach (var person in _repository.GetPeople())
                {
                    if (person == null)
                        continue;

                    int? orgId = null;
                    string orgName = null;
                    try
                    {
                        if (person.MainPosition != null)
                        {
                            orgId = person.MainPosition.Position;
                            OrganisationInventoryRecord org;
                            if (orgs != null && orgId.HasValue && orgs.TryGetValue(orgId.Value, out org))
                                orgName = org.Name;
                            else if (orgId.HasValue)
                            {
                                var ou = _repository.GetOrganisationUnit(orgId.Value);
                                if (ou != null)
                                    orgName = ou.Title;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AnalyticsLogger.Warning("person-org", "person=" + person.Id + " " + ex.Message);
                    }

                    list.Add(new PersonInventoryRecord
                    {
                        PersonId = person.Id,
                        DisplayName = string.IsNullOrWhiteSpace(person.DisplayName) ? person.ActualName : person.DisplayName,
                        OrganisationId = orgId,
                        OrganisationName = orgName,
                        IsDeleted = person.IsDeleted
                    });
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("person-discovery", ex);
                throw;
            }

            return list.OrderBy(p => p.DisplayName).ToList();
        }
    }

    internal sealed class OrganisationDiscoveryService
    {
        private readonly IObjectsRepository _repository;

        public OrganisationDiscoveryService(IObjectsRepository repository)
        {
            _repository = repository;
        }

        public List<OrganisationInventoryRecord> DiscoverOrganisations()
        {
            var list = new List<OrganisationInventoryRecord>();
            var parentMap = new Dictionary<int, int>();

            try
            {
                var all = _repository.GetOrganisationUnits().Where(o => o != null).ToList();
                foreach (var org in all)
                {
                    if (org.Children == null)
                        continue;
                    foreach (var childId in org.Children)
                    {
                        if (!parentMap.ContainsKey(childId))
                            parentMap[childId] = org.Id;
                    }
                }

                foreach (var org in all)
                {
                    int parentId;
                    int? parent = parentMap.TryGetValue(org.Id, out parentId) ? parentId : (int?)null;
                    list.Add(new OrganisationInventoryRecord
                    {
                        OrganisationId = org.Id,
                        Name = org.Title,
                        ParentOrganisationId = parent,
                        IsPosition = org.IsPosition,
                        IsDeleted = org.IsDeleted,
                        ChildrenCount = org.Children != null ? org.Children.Count : 0
                    });
                }
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("org-discovery", ex);
                throw;
            }

            return list.OrderBy(o => o.Name).ToList();
        }
    }
}
