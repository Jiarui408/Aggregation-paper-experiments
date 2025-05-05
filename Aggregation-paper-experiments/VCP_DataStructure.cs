using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using System.Xml.Linq;
using System.Windows.Forms;

namespace Aggregation_paper_experiments
{
	class VCP_DataStructure
	{
		//模型相关参数
		public double gap = 0.0;
		public int time = 3600;
		public int rollingstock_quantity = 0;

		//输入数据存储
		public Dictionary<int, Demand> demands = new Dictionary<int, Demand>();
		public Dictionary<string, Node> nodes = new Dictionary<string, Node>();
		public Dictionary<string, Arc> arcs = new Dictionary<string, Arc>();
		public Dictionary<string, HashSet<int>> sta_timeunits = new Dictionary<string, HashSet<int>>();
		public Dictionary<int, RollingStock> rollingstocks = new Dictionary<int, RollingStock>();
		public Dictionary<string, int> arc_quantity = new Dictionary<string, int>();

		//输出数据
		public double aggregation_solving_time = 0.0;
		public double disaggregation_solving_time = 0.0;
		public double original_solving_time = 0.0;
		public double aggregation_gap = 100.0;
		public double original_gap = 100.0;

		#region 数据读取构建网络
		public void ReadDataAggregationPaper(int i)
		{
			//读取货流
			FileStream fs1 = new FileStream("VCP_instances\\" + "day" + i.ToString() + ".csv", FileMode.Open);
			StreamReader sr1 = new StreamReader(fs1);
			sr1.ReadLine();
			string NoteData1 = sr1.ReadLine();
			int commodity_key = 1;
			while (NoteData1 != null && NoteData1 != "")
			{
				string[] data = new string[5];
				data = NoteData1.Split(',');
				Demand d = new Demand();
				d.name = commodity_key;
				d.startstation = data[1].ToString();
				d.endstation = data[2].ToString();
				d.starttime = Convert.ToInt32(data[3]);
				d.endtime = Convert.ToInt32(data[4]);
				demands[commodity_key] = d;

				if (sta_timeunits.ContainsKey(d.startstation) == false)
					sta_timeunits[d.startstation] = new HashSet<int>();
				sta_timeunits[d.startstation].Add(d.starttime);
				if (sta_timeunits.ContainsKey(d.endstation) == false)
					sta_timeunits[d.endstation] = new HashSet<int>();
				sta_timeunits[d.endstation].Add(d.endtime);

				Buildnode(d.startstation, d.starttime);
				Buildnode(d.endstation, d.endtime);
				BuildDemandArc(d);

				NoteData1 = sr1.ReadLine();
				commodity_key++;
			}
			sr1.Close();
			fs1.Close();

			void Buildnode(string station, int time)
			{
				string name = station + "-" + time.ToString();
				//建立节点
				if (nodes.ContainsKey(name) == false)
				{
					Node n = new Node();
					n.name = name;
					n.nodestation = station;
					n.nodetime = time;
					nodes[name] = n;
				}
			}
			void BuildDemandArc(Demand d)
			{
				Arc a1 = new Arc();
				string startnodename = d.startstation + "-" + d.starttime;
				string endnodename = d.endstation + "-" + d.endtime;
				a1.name = startnodename + "-" + endnodename;
				a1.outnode = nodes[startnodename];
				a1.innode = nodes[endnodename];
				a1.mark = "demand";
				arcs[a1.name] = a1;
				nodes[startnodename].outarc.Add(a1);
				nodes[endnodename].inarc.Add(a1);
			}
		}
		public void ConstructNetwork()
		{
			Node n1 = new Node();
			n1.name = "start";
			nodes[n1.name] = n1;

			Node n2 = new Node();
			n2.name = "end";
			nodes[n2.name] = n2;

			Dictionary<string, List<int>> sta_timesequence = new Dictionary<string, List<int>>();
			//补全axis弧段
			foreach (var v in sta_timeunits)
			{
				sta_timesequence[v.Key] = new List<int>();
				List<int> temp = new List<int>();
				temp = v.Value.ToList();
				temp.Sort();
				sta_timesequence[v.Key] = temp;
			}
			foreach (var v in sta_timesequence)
			{
				string sta = v.Key;
				for (int key = 0; key <= v.Value.Count - 2; key++)
				{
					Arc a1 = new Arc();
					string startnodename = sta + "-" + v.Value[key];
					string endnodename = sta + "-" + v.Value[key + 1];
					a1.name = startnodename + "-" + endnodename;
					a1.outnode = nodes[startnodename];
					a1.innode = nodes[endnodename];
					a1.mark = "axis";
					arcs[a1.name] = a1;
					nodes[startnodename].outarc.Add(a1);
					nodes[endnodename].inarc.Add(a1);
				}
			}

			//补全虚拟链接弧
			for (int i = 1; i <= 1; i++)
			{
				Arc a3 = new Arc();
				string startnodename = "start";
				string endnodename = "end";
				a3.name = startnodename + "-" + endnodename;
				a3.outnode = nodes[startnodename];
				a3.innode = nodes[endnodename];
				a3.mark = "virtualcon";
				arcs[a3.name] = a3;
				nodes[startnodename].outarc.Add(a3);
				nodes[endnodename].inarc.Add(a3);
			}
			foreach (var v in sta_timesequence)
			{
				Arc a1 = new Arc();
				string startnodename = "start";
				string endnodename = v.Key + "-" + v.Value[0];
				a1.name = startnodename + "-" + endnodename;
				a1.outnode = nodes[startnodename];
				a1.innode = nodes[endnodename];
				a1.mark = "virtualstart";
				arcs[a1.name] = a1;
				nodes[startnodename].outarc.Add(a1);
				nodes[endnodename].inarc.Add(a1);

				Arc a2 = new Arc();
				startnodename = v.Key + "-" + v.Value[v.Value.Count - 1];
				endnodename = "end";
				a2.name = startnodename + "-" + endnodename;
				a2.outnode = nodes[startnodename];
				a2.innode = nodes[endnodename];
				a2.mark = "virtualend";
				arcs[a2.name] = a2;
				nodes[startnodename].outarc.Add(a2);
				nodes[endnodename].inarc.Add(a2);
			}

			int temp_key = 1;
			foreach (Demand d in demands.Values)
			{
				RollingStock rs = new RollingStock();
				rs.name = temp_key;
				rollingstocks[temp_key] = rs;
				temp_key++;
			}
		}
		#endregion

	}

	class RollingStock()
	{
		public int name = 0;
	}
}
