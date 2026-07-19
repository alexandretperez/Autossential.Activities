namespace Autossential.Activities.Tests.Activities
{
    public class BaseTests
    {
        private string _dir;
        protected string NewDir()
        {
            _dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(_dir);
            return _dir;
        }

        [After(Test)]
        public void Delete()
        {
            if (!string.IsNullOrEmpty(_dir))
                try { Directory.Delete(_dir, true); } catch { }
        }
    }
}