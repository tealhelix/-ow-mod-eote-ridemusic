using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMusic
{
    class Sequence
    {
        public List<float[]> _beats = new List<float[]>();
        public int _index = 0;

        public float[] Next(float lastTime, float curTime)
        {
            if (_index >= _beats.Count)
                return null;
            var beatTime = _beats[_index][0];
            if (lastTime <= beatTime && beatTime < curTime) // between
                return _beats[_index++];    // advance sequence
            else
                return null;
        }
        public void Reset()
        {
            _index = 0;
        }
    }
}
