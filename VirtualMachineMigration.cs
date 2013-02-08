using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using AppUtil;
using VimApi;
using System.Threading;
using System.Web.Services.Protocols;

namespace PropertyCollector
{
    public class PropertyCollector
    {
        static VimService _service;
        static ServiceContent _sic;
        private static AppUtil.AppUtil cb = null;
        Log log = new Log();
        private static OptionSpec[] constructOptions()
        {
            OptionSpec[] useroptions = new OptionSpec[2];
            useroptions[0] = new OptionSpec("dcName","String",1,"Name of the Datacenter",null);
            useroptions[1] = new OptionSpec("vmName","String",1,"Virtual machine dns name",null);
            return useroptions;
        }

        static ObjectContent[] getVMInfo(ManagedObjectReference vmMoRef)
        {
            // This spec selects VirtualMachine information
            PropertySpec vmPropSpec = new PropertySpec();
            vmPropSpec.type = "VirtualMachine";
            vmPropSpec.pathSet = new String[]{"name","config.guestFullName","summary.quickStats.overallCpuUsage","summary.quickStats.hostMemoryUsage","summary.quickStats.guestMemoryUsage"};
            PropertySpec hostPropSpec = new PropertySpec();
            hostPropSpec.type = "HostSystem";
            hostPropSpec.pathSet = new String[] { "name", "summary.quickStats.overallCpuUsage", "summary.quickStats.overallMemoryUsage", };
            TraversalSpec hostTraversalSpec = new TraversalSpec();
            hostTraversalSpec.type = "VirtualMachine";
            hostTraversalSpec.path = "runtime.host";
            ObjectSpec oSpec = new ObjectSpec();
            oSpec.obj = vmMoRef;
            oSpec.selectSet = new SelectionSpec[]{hostTraversalSpec};
            PropertyFilterSpec pfSpec = new PropertyFilterSpec();
            pfSpec.propSet = new PropertySpec[]{vmPropSpec,hostPropSpec};
            pfSpec.objectSet = new ObjectSpec[]{oSpec};
            return _service.RetrieveProperties(_sic.propertyCollector,new PropertyFilterSpec[]{pfSpec});
        }

        static void printVmInfo(ObjectContent[] ocs, ref int a, ref int b,ref string content,FileStream fs,ref string sHost)
        {
            StreamWriter sw = new StreamWriter(fs);
            if (ocs != null)
            {
                for (int oci = 0; oci < ocs.Length; ++oci)
                {
                    ObjectContent oc = ocs[oci];
                    String type = oc.obj.type;
                    Console.WriteLine("VM Information");
                    Console.WriteLine(DateTime.Now.ToString());
                    sw.WriteLine(DateTime.Now.ToString());
                    sw.WriteLine("VM Information");
                    if ("VirtualMachine".Equals(type))
                    {
                        DynamicProperty[] dps = oc.propSet;
                        if (dps != null)
                        {
                            for (int j = 0; j < dps.Length; ++j)
                            {
                                DynamicProperty dp = dps[j];
                                if ("name".Equals(dp.name))
                                {
                                    Console.WriteLine("  Name               : " + (String)dp.val);
                                    sw.WriteLine("  Name               : " + dp.val.ToString());
                                }
                                else if ("config.guestFullName".Equals(dp.name))
                                {
                                    Console.WriteLine("  Guest OS Name      : " + (String)dp.val);
                                    sw.WriteLine("  Guest OS Name      : " + dp.val.ToString());
                                }
                                else if ("summary.quickStats.overallCpuUsage".Equals(dp.name))
                                {                         
                                    Console.WriteLine("  CPU Usage          : " + (int)dp.val + " MHz");
                                    sw.WriteLine("  CPU Usage          : " + dp.val.ToString() + " MHz");
                                }
                                else if ("summary.quickStats.hostMemoryUsage".Equals(dp.name))
                                {
                                    Console.WriteLine("  Host Memory Usage  : " + (int)dp.val + " MB");
                                    sw.WriteLine("  Host Memory Usage  : " + dp.val.ToString() + " MB");
                                }
                                    else if ("summary.quickStats.guestMemoryUsage".Equals(dp.name))
                                {
                                    Console.WriteLine("  Guest Memory Usage : " + (int)dp.val + " MB");
                                    sw.WriteLine("  Guest Memory Usage : " + dp.val.ToString() + " MB");
                                }
                            }
                        }
                    }
                    else if ("HostSystem".Equals(type))
                    {
                        DynamicProperty[] dps = oc.propSet;
                        if (dps != null)
                        {
                            for (int j = 0; j < dps.Length; ++j)
                            {
                                DynamicProperty dp = dps[j];
                                if ("name".Equals(dp.name))
                                {
                                    Console.WriteLine("  Host               : " + (String)dp.val);
                                    sw.WriteLine("  Host               : " + dp.val.ToString());
                                    sHost = dp.val.ToString();
                                }
                                else if ("summary.quickStats.overallCpuUsage".Equals(dp.name))
                                {
                                    a += (int)dp.val;
                                    Console.WriteLine("  CPU Usage          : " + (int)dp.val + " MHz");
                                    sw.WriteLine("  CPU Usage          : " + dp.val.ToString() + " MHz");
                                }
                                else if ("summary.quickStats.overallMemoryUsage".Equals(dp.name))
                                {
                                    b += (int)dp.val;
                                    Console.WriteLine("  Memory Usage       : " + (int)dp.val + " MB");
                                    sw.WriteLine("  Memory Usage       : " + dp.val.ToString() + " MB");
                                }
                            }
                        }
                    }
                }
            }
            sw.Flush();
        }

        static private ManagedObjectReference getMOR(String name, String type, ManagedObjectReference root)
        {

            ManagedObjectReference nameMOR = (ManagedObjectReference)cb.getServiceUtil().GetDecendentMoRef(root, type, name);
            if (nameMOR == null)
            {
                Console.WriteLine("Error:: " + name + " not found");
                return null;
            }
            else
            {
                return nameMOR;
            }
        }

        static public void migrateVM(String vmname, String pool, String tHost, String srcHost)
        {
            //String priority;
            VirtualMachinePowerState st = VirtualMachinePowerState.poweredOn;
            VirtualMachineMovePriority pri = VirtualMachineMovePriority.highPriority;
            try
            {
                ManagedObjectReference srcMOR = getMOR(srcHost, "HostSystem", null);
                ManagedObjectReference vmMOR = getMOR(vmname, "VirtualMachine", srcMOR);
                ManagedObjectReference poolMOR = getMOR(pool, "ResourcePool", null);
                ManagedObjectReference hMOR = getMOR(tHost, "HostSystem", null);
                if (vmMOR == null || srcMOR == null || poolMOR == null || hMOR == null)
                {
                    return;
                }

                Console.WriteLine("Migrating the Virtual Machine " + vmname);
                ManagedObjectReference taskMOR = cb.getConnection().Service.MigrateVM_Task(vmMOR, poolMOR, hMOR, pri, st, true);
                String res = cb.getServiceUtil().WaitForTask(taskMOR);
                if (res.Equals("sucess"))
                {
                    Console.WriteLine("Migration of Virtual Machine " + vmname + " done successfully to " + tHost);
                }
                else
                {
                    Console.WriteLine("Error::  Migration failed");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
        
        public static void Main(String[] args)
        {
            string sHost = "";
            int totalCpuUsage = 0;
            int totalMemUsage = 0;
            string content="";
            FileStream fs = null;
            if(!File.Exists("../../Output.txt"))
            {
                 fs = new FileStream("../../Output.txt",FileMode.Create);
            }
            else   
            {
                 fs = new FileStream("../../Output.txt",FileMode.Append);
            }
                      
            try
            {
                PropertyCollector app = new PropertyCollector();
                cb = AppUtil.AppUtil.initialize("PropertyCollector", PropertyCollector.constructOptions(), args);
                cb.connect();
                ManagedObjectReference sic = cb.getConnection().ServiceRef;
                _sic = cb.getConnection()._sic;
                _service = cb.getConnection()._service;
                String dcName = cb.get_option("dcName");
                String vmName = cb.get_option("vmName");
                ObjectContent[] ocs = null;
                ManagedObjectReference dcMoRef = cb.getServiceUtil().GetDecendentMoRef(null, "Datacenter", dcName);
                if (dcMoRef == null)
                {
                    Console.WriteLine("Datacenter not found");
                }
                else
                {
                    ManagedObjectReference vmMoRef = cb.getServiceUtil().GetDecendentMoRef(null, "VirtualMachine", vmName);
                    if (vmMoRef == null)
                    {
                        Console.WriteLine("The virtual machine with DNS '" + vmName + "' not found ");
                    }
                    else
                    {
                        // Display summary information about a VM
                        while(true)
                        {
                            for (int times = 0; times < 12; times++)
                            {
                                ocs = getVMInfo(vmMoRef);
                                printVmInfo(ocs, ref totalCpuUsage, ref totalMemUsage, ref content, fs, ref sHost);
                                Console.WriteLine("\n The system will halt for 15 seconds \n");
                                Thread.Sleep(15 * 1000);
                            }
                            if ((double)(totalCpuUsage / 120) > 50 || (double)(totalMemUsage) / 12 > 300)
                            {
                                try
                                {
                                    string vmname = "jwang74_2";
                                    string pool = "jwang74";
                                    string[] tHostArray = { "128.230.96.39", "128.230.96.41", "128.230.96.39", "128.230.96.40", "128.230.96.111" };
                                    string tHost = "";
                                    do
                                    {
                                        Random a = new Random();
                                        int i = a.Next(0, 5);
                                        tHost = tHostArray[i];
                                    } while (tHost == sHost);
                                    migrateVM(vmname, pool, tHost, sHost);
                                    Console.WriteLine("Do you want to continue: Y/N");
                                    string con = Console.ReadLine();
                                    if (con.Equals("Y") || con.Equals("y"))
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                                catch (Exception)
                                {
                                }
                                Console.WriteLine("Press any key to exit: ");
                                Console.Read();
                            }
                            Console.WriteLine("Do you want to continue: Y/N");
                            string condition = Console.ReadLine();
                            if (condition.Equals("Y") || condition.Equals("y"))
                            {
                                continue;
                            }
                            else
                            {
                                break;
                            }
                        }
                
                        cb.disConnect();
                        Console.WriteLine("Press any key to exit:");
                        Console.Read();
                    }
                }
            }
            catch (SoapException e)
            {
                if (e.Detail.FirstChild.LocalName.Equals("DuplicateNameFault"))
                {
                    Console.WriteLine("Managed Entity with the name already exists");
                }
                else if (e.Detail.FirstChild.LocalName.Equals("InvalidArgumentFault"))
                {
                    Console.WriteLine("Specification is invalid");
                }
                else if (e.Detail.FirstChild.LocalName.Equals("InvalidNameFault"))
                {
                    Console.WriteLine("Managed Entity Name is empty or too long");
                }
                else if (e.Detail.FirstChild.LocalName.Equals("RuntimeFault"))
                {
                    Console.WriteLine(e.Message.ToString() + "Either parent name or item name is invalid");
                }
                else if (e.Detail.FirstChild.LocalName.Equals("RuntimeFault"))
                {
                    Console.WriteLine(e.Message.ToString() + " " + "The Operation is not supported on this object");
                }
                else
                {
                    Console.WriteLine(e.Message.ToString() + " " + "The Operation is not supported on this object");
                }
            }
            Console.Read();
            fs.Close();
        }
    }
}
