namespace DiceCraft
{
    public sealed partial class DiceTable
    {
        /// <summary>
        /// v1.1.1 (task W7b, RULES_DELTA 7.3): takes one die off the table at once, while the others stay as they are.
        /// A glass die that broke after a submission leaves this way on the beat its shatter starts; the next
        /// Display of the same die shows it again. Returns false when the die is not on the table.
        /// </summary>
        public bool HideDie111(string dieId)
        {
            if (dieId == null || !visuals.TryGetValue(dieId, out Visual visual) || visual.root == null) return false;
            bool shown = visual.root.activeSelf;
            visual.root.SetActive(false);
            return shown;
        }
    }
}
