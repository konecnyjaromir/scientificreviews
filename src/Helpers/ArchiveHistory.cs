using Newtonsoft.Json;
using ScientificReviews.Bibtex;
using System.Collections.Generic;

namespace ScientificReviews.Helpers
{
    public sealed class ArchiveHistory
    {
        private const int MaxStates = 51;
        private const int MaxCharacters = 16 * 1024 * 1024;
        private readonly List<string> _states = new List<string>();
        private int _position;
        private string _savedState;

        public bool CanUndo => _position > 0;
        public bool CanRedo => _position < _states.Count - 1;
        public bool IsSaved => _states.Count > 0 && _states[_position] == _savedState;

        public void Reset(IEnumerable<BibtexEntry> entries, bool isSaved = true)
        {
            _states.Clear();
            _states.Add(Serialize(entries));
            _position = 0;
            _savedState = isSaved ? _states[0] : null;
        }

        public void Record(IEnumerable<BibtexEntry> entries)
        {
            string state = Serialize(entries);
            if (_states.Count == 0)
            {
                Reset(entries);
                return;
            }
            if (_states[_position] == state)
                return;

            _states.RemoveRange(_position + 1, _states.Count - _position - 1);
            _states.Add(state);
            _position++;

            long characters = 0;
            foreach (string item in _states)
                characters += item.Length;
            // Always retain the current state and one undo step, even for large archives.
            while (_states.Count > 2 && (_states.Count > MaxStates || characters > MaxCharacters))
            {
                characters -= _states[0].Length;
                _states.RemoveAt(0);
                _position--;
            }
        }

        public void MarkSaved() => _savedState = _states[_position];

        public List<BibtexEntry> Undo()
        {
            if (!CanUndo)
                return null;
            return Deserialize(_states[--_position]);
        }

        public List<BibtexEntry> Redo()
        {
            if (!CanRedo)
                return null;
            return Deserialize(_states[++_position]);
        }

        private static string Serialize(IEnumerable<BibtexEntry> entries) =>
            JsonConvert.SerializeObject(entries ?? new BibtexEntry[0]);

        private static List<BibtexEntry> Deserialize(string state) =>
            JsonConvert.DeserializeObject<List<BibtexEntry>>(state);
    }
}
