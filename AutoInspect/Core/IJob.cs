using AutoInspect.Helper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoInspect.Core
{
    public interface IJob
    {
        TaskEnum Id { get; }
        Task Invoke();
    }
}
