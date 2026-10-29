using System.Collections;
bool restored = false, failure = false;
IEnumerator Child() { yield return null; throw new InvalidOperationException("resolution timeout fixture"); }
IEnumerator Parent() { try { yield return Child(); } finally { restored = true; } }
// Historical pre-extension Guard pattern contrasted with the current explicit
// LIFO cleanup. This is not a finding against current source 80254a91... .
// Unity APIs are not needed to exercise C# iterator disposal semantics.
IEnumerator Guard(IEnumerator work, bool cleanup)
{
    var stack = new Stack<IEnumerator>(); stack.Push(work);
    while (stack.Count > 0)
    {
        object next = null; Exception error = null; bool moved = false;
        try { moved = stack.Peek().MoveNext(); if (moved) next = stack.Peek().Current; }
        catch (Exception ex) { error = ex; }
        if (error != null)
        {
            if (cleanup) while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception ex) { error = new AggregateException(error, ex); }
            }
            failure = true; yield break;
        }
        if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
        if (next is IEnumerator nested) stack.Push(nested); else yield return next;
    }
}
var guard = Guard(Parent(), false); while (guard.MoveNext()) { }
(guard as IDisposable)?.Dispose();
Console.WriteLine($"Historical pattern: failure={failure}; outer finally restored={restored}");
if (!failure || restored) return 1;
failure = restored = false;
guard = Guard(Parent(), true); while (guard.MoveNext()) { }
(guard as IDisposable)?.Dispose();
Console.WriteLine($"Current LIFO cleanup pattern: failure={failure}; outer finally restored={restored}");
return failure && restored ? 0 : 1;
