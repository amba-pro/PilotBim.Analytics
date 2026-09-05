using System;
using System.Collections.Generic;
using Ascon.Pilot.SDK;
using PilotBim.Analytics.Diagnostics;

namespace PilotBim.Analytics.Data
{
    internal sealed class ActionObserver<T> : IObserver<T>
    {
        private readonly Action<T> _onNext;
        private readonly Action<Exception> _onError;
        private readonly Action _onCompleted;

        public ActionObserver(Action<T> onNext, Action<Exception> onError = null, Action onCompleted = null)
        {
            _onNext = onNext;
            _onError = onError;
            _onCompleted = onCompleted;
        }

        public void OnNext(T value)
        {
            try
            {
                if (_onNext != null)
                    _onNext(value);
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("observer-onnext", ex);
            }
        }

        public void OnError(Exception error)
        {
            if (_onError != null)
                _onError(error);
            else
                AnalyticsLogger.Error("observer-onerror", error);
        }

        public void OnCompleted()
        {
            if (_onCompleted != null)
                _onCompleted();
        }
    }

    internal sealed class PilotObjectScanner
    {
        private readonly IObjectsRepository _repository;
        private readonly ISearchService _search;

        public PilotObjectScanner(IObjectsRepository repository, ISearchService search)
        {
            _repository = repository;
            _search = search;
        }

        public bool SearchAvailable
        {
            get { return _search != null; }
        }

        /// <summary>
        /// Searches object IDs by type. Uses ISearchResult.Total when available.
        /// </summary>
        public void SearchByType(
            int typeId,
            int maxResults,
            Action<IReadOnlyList<Guid>, long> onDone,
            Action<Exception> onError = null)
        {
            if (_search == null)
            {
                onDone(new List<Guid>(), -1);
                return;
            }

            try
            {
                var query = _search.GetObjectQueryBuilder()
                    .Must(ObjectFields.TypeId.Be(typeId))
                    .MaxResults(maxResults);

                var finished = false;
                Action<IReadOnlyList<Guid>, long> done = (ids, total) =>
                {
                    if (finished)
                        return;
                    finished = true;
                    onDone(ids, total);
                };

                _search.Search(query).Subscribe(new ActionObserver<ISearchResult>(
                    result =>
                    {
                        if (result == null)
                        {
                            done(new List<Guid>(), 0);
                            return;
                        }

                        var ids = result.Result != null
                            ? new List<Guid>(result.Result)
                            : new List<Guid>();
                        done(ids, result.Total);
                    },
                    ex =>
                    {
                        if (onError != null)
                            onError(ex);
                        done(new List<Guid>(), -1);
                    },
                    () => done(new List<Guid>(), 0)));
            }
            catch (Exception ex)
            {
                if (onError != null)
                    onError(ex);
                onDone(new List<Guid>(), -1);
            }
        }

        public IDisposable SubscribeObjects(
            IEnumerable<Guid> ids,
            Action<IDataObject> onObject,
            Action onCompleted = null,
            Action<Exception> onError = null)
        {
            return _repository.SubscribeObjects(ids)
                .Subscribe(new ActionObserver<IDataObject>(onObject, onError, onCompleted));
        }

        /// <summary>
        /// Synchronously load one object (cache first, then SubscribeObjects).
        /// </summary>
        public IDataObject SubscribeObject(Guid id, TimeSpan timeout)
        {
            if (id == Guid.Empty)
                return null;

            var cached = TryGetCached(id);
            if (cached != null)
                return cached;

            IDataObject loaded = null;
            using (var gate = new System.Threading.ManualResetEventSlim(false))
            {
                try
                {
                    SubscribeObjects(
                        new[] { id },
                        obj =>
                        {
                            if (obj == null || obj.Id != id)
                                return;
                            if (obj.State == DataState.Loaded)
                                loaded = obj;
                            gate.Set();
                        },
                        onCompleted: () => gate.Set(),
                        onError: ex =>
                        {
                            AnalyticsLogger.Warning("subscribe-object", id + " " + (ex != null ? ex.Message : ""));
                            gate.Set();
                        });
                    gate.Wait(timeout);
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Error("subscribe-object", ex);
                }
            }

            return loaded ?? TryGetCached(id);
        }

        /// <summary>
        /// Synchronously prefetch a batch into cache (best-effort).
        /// </summary>
        public void SubscribeObjects(IEnumerable<Guid> ids, TimeSpan timeout)
        {
            if (ids == null)
                return;

            var list = new List<Guid>();
            foreach (var id in ids)
            {
                if (id == Guid.Empty)
                    continue;
                if (TryGetCached(id) != null)
                    continue;
                list.Add(id);
            }

            if (list.Count == 0)
                return;

            var remaining = new HashSet<Guid>(list);
            using (var gate = new System.Threading.ManualResetEventSlim(false))
            {
                try
                {
                    SubscribeObjects(
                        list,
                        obj =>
                        {
                            if (obj == null)
                                return;
                            remaining.Remove(obj.Id);
                            if (remaining.Count == 0)
                                gate.Set();
                        },
                        onCompleted: () => gate.Set(),
                        onError: ex =>
                        {
                            AnalyticsLogger.Warning("subscribe-batch", ex != null ? ex.Message : "");
                            gate.Set();
                        });
                    gate.Wait(timeout);
                }
                catch (Exception ex)
                {
                    AnalyticsLogger.Error("subscribe-batch", ex);
                }
            }
        }

        public IDataObject TryGetCached(Guid id)
        {
            try
            {
#pragma warning disable 612
                var obj = _repository.GetCachedObject(id);
#pragma warning restore 612
                if (obj != null && obj.State == DataState.Loaded)
                    return obj;
            }
            catch (NotSupportedException)
            {
            }
            catch (Exception ex)
            {
                AnalyticsLogger.Error("get-cached", ex);
            }

            return null;
        }
    }

    internal sealed class PilotObjectSampler
    {
        private readonly PilotObjectScanner _scanner;

        public PilotObjectSampler(PilotObjectScanner scanner)
        {
            _scanner = scanner;
        }

        public void SampleType(
            int typeId,
            int sampleLimit,
            Action<IReadOnlyList<IDataObject>, long> onDone,
            Action<Exception> onError = null)
        {
            _scanner.SearchByType(typeId, sampleLimit, (ids, total) =>
            {
                if (ids == null || ids.Count == 0)
                {
                    onDone(new List<IDataObject>(), total);
                    return;
                }

                var loaded = new List<IDataObject>();
                var remaining = new HashSet<Guid>(ids);
                var finished = false;
                Action done = () =>
                {
                    if (finished)
                        return;
                    finished = true;
                    onDone(loaded, total);
                };

                _scanner.SubscribeObjects(
                    ids,
                    obj =>
                    {
                        if (obj == null || !remaining.Remove(obj.Id))
                            return;
                        if (obj.State == DataState.Loaded)
                            loaded.Add(obj);
                        if (remaining.Count == 0)
                            done();
                    },
                    onCompleted: done,
                    onError: ex =>
                    {
                        if (onError != null)
                            onError(ex);
                        done();
                    });
            }, onError);
        }
    }

    internal static class ReferenceResolver
    {
        public static bool TryGetPersonId(object value, out int personId)
        {
            personId = 0;
            if (value == null)
                return false;

            if (value is int i)
            {
                personId = i;
                return personId != 0;
            }

            if (value is long l && l <= int.MaxValue && l >= int.MinValue)
            {
                personId = (int)l;
                return personId != 0;
            }

            // OrgUnit attributes often store int[] of organisation unit ids (positions).
            return false;
        }

        public static IEnumerable<int> EnumerateIntIds(object value)
        {
            if (value == null)
                yield break;

            if (value is int i)
            {
                yield return i;
                yield break;
            }

            if (value is long l && l <= int.MaxValue && l >= int.MinValue)
            {
                yield return (int)l;
                yield break;
            }

            var array = value as Array;
            if (array != null)
            {
                foreach (var item in array)
                {
                    foreach (var nested in EnumerateIntIds(item))
                        yield return nested;
                }
                yield break;
            }

            var list = value as System.Collections.IEnumerable;
            if (list != null && !(value is string))
            {
                foreach (var item in list)
                {
                    foreach (var nested in EnumerateIntIds(item))
                        yield return nested;
                }
            }
        }

        public static bool TryGetGuid(object value, out Guid guid)
        {
            guid = Guid.Empty;
            if (value == null)
                return false;
            if (value is Guid g)
            {
                guid = g;
                return guid != Guid.Empty;
            }

            var s = value as string;
            if (!string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out g))
            {
                guid = g;
                return guid != Guid.Empty;
            }

            return false;
        }

        public static string SafeSampleString(object value, int maxLen = 80)
        {
            if (value == null)
                return null;

            try
            {
                string text;
                if (value is string s)
                    text = s;
                else if (value is Guid g)
                    text = g.ToString();
                else if (value is DateTime dt)
                    text = dt.ToString("o");
                else if (value is Array arr)
                    text = "[" + arr.Length + " items]";
                else if (value is System.Collections.IEnumerable && !(value is string))
                    text = value.GetType().Name;
                else
                    text = Convert.ToString(value);

                if (string.IsNullOrWhiteSpace(text))
                    return null;
                text = text.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (text.Length > maxLen)
                    return text.Substring(0, maxLen) + "...";
                return text;
            }
            catch
            {
                return "<unreadable>";
            }
        }

        public static bool IsEmptyValue(object value)
        {
            if (value == null)
                return true;
            if (value is string s)
                return string.IsNullOrWhiteSpace(s);
            if (value is Guid g)
                return g == Guid.Empty;
            var arr = value as Array;
            if (arr != null)
                return arr.Length == 0;
            return false;
        }
    }
}
