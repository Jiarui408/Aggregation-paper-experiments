using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace Aggregation_paper_experiments
{
	class SND_DataStructure
	{
		//模型相关参数
		public int service_capacity = 29;
		public double service_cost_perlength = 7.93;
		public double demand_arc_cost_perlength = 0.33;
		public int max_service_frequency = 200;

		public double gap = 0.0;
		public int time = 3600;

		public HashSet<string> origins = new HashSet<string>();
		public HashSet<string> destinations = new HashSet<string>();

		//输入数据存储
		public Dictionary<int, Demand> demands = new Dictionary<int, Demand>();
		public Dictionary<string, AggregateFlow> aggregate_flows = new Dictionary<string, AggregateFlow>();
		public Dictionary<string, Node> nodes = new Dictionary<string, Node>();
		public Dictionary<string, Arc> arcs = new Dictionary<string, Arc>();
		public Dictionary<string, Dictionary<string, int>> aggregateflow_arc_quanity = new Dictionary<string, Dictionary<string, int>>();

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
			FileStream fs1 = new FileStream("SND_instances\\" + "week_" + i.ToString() + ".csv", FileMode.Open);
			StreamReader sr1 = new StreamReader(fs1);
			sr1.ReadLine();
			string NoteData1 = sr1.ReadLine();
			int commodity_key = 1;
			while (NoteData1 != null && NoteData1 != "")
			{
				string[] data = new string[4];
				data = NoteData1.Split(',');
				Demand d = new Demand();
				d.name = commodity_key;
				d.startstation = data[0].ToString();
				d.endstation = data[1].ToString();
				d.amount = Convert.ToInt32(data[2]);
				d.distance = Convert.ToInt32(data[3]);
				demands[commodity_key] = d;

				origins.Add(d.startstation);
				destinations.Add(d.endstation);

				Buildnode(d.startstation);
				Buildnode(d.endstation);
				BuildArc(d.startstation, d.endstation, d.distance, false);

				NoteData1 = sr1.ReadLine();
				commodity_key++;
			}
			sr1.Close();
			fs1.Close();

			void Buildnode(string name)
			{
				//建立节点
				if (nodes.ContainsKey(name) == false)
				{
					Node n = new Node();
					n.name = name;
					nodes[name] = n;
				}
			}
			void BuildArc(string name1, string name2, int distance, bool two_direction)
			{
				Arc a1 = new Arc();
				a1.name = name1 + "-" + name2;
				a1.outnode = nodes[name1];
				a1.innode = nodes[name2];
				a1.length = distance;
				nodes[name1].outarc.Add(a1);
				nodes[name2].inarc.Add(a1);
				arcs[a1.name] = a1;

				if (two_direction == true)
				{
					Arc a2 = new Arc();
					a2.name = name2 + "-" + name1;
					a2.outnode = nodes[name2];
					a2.innode = nodes[name1];
					a2.length = distance;
					arcs[a2.name] = a2;
					nodes[name2].outarc.Add(a2);
					nodes[name1].inarc.Add(a2);
				}
			}
		}
		public void FindAggregation()
		{
			foreach (var v in demands)
			{
				string origin = v.Value.startstation;
				if (aggregate_flows.ContainsKey(origin) == false)
				{
					AggregateFlow af = new AggregateFlow();
					af.origin = origin;
					aggregate_flows[origin] = af;
				}
				aggregate_flows[origin].ori_amount = aggregate_flows[origin].ori_amount + v.Value.amount;
				aggregate_flows[origin].including_commodities.Add(v.Value.name);
				if (aggregate_flows[origin].des_amount.ContainsKey(v.Value.endstation) == false)
					aggregate_flows[origin].des_amount[v.Value.endstation] = 0;
				aggregate_flows[origin].des_amount[v.Value.endstation] = aggregate_flows[origin].des_amount[v.Value.endstation] + v.Value.amount;
			}
		}
		#endregion
	}

	class Arc
	{
		/// <summary>
		/// 弧的名字
		/// </summary>
		public string name;
		/// <summary>
		/// 弧的类型，trans\pass\load\unload\train
		/// </summary>
		public string mark;
		/// <summary>
		/// 使用这个弧会带来的花费
		/// </summary>
		public double length;
		/// <summary>
		/// 此弧从哪个点出 
		/// </summary>
		public Node outnode;
		/// <summary>
		/// 此弧进入哪个点
		/// </summary>
		public Node innode;
		/// <summary>
		/// 弧的运输能力Ca
		/// </summary>
		public int capacity;
		/// <summary>
		/// 货物运输弧段的乘子rho
		/// </summary>
		public double roll = 0.0;
		/// <summary>
		/// 某一个运输弧段对应的班列弧段
		/// </summary>
		public string belonging_trainarc_name = "";
		/// <summary>
		/// 班列弧所包含的运输弧段名称集合
		/// </summary>
		public List<string> including_freightarcs = new List<string>();
		/// <summary>
		/// 班列弧对应的capacity
		/// </summary>
		public HashSet<string> related_resourcenames = new HashSet<string>();

		/// <summary>
		/// for HISP-MP
		/// </summary>
		public int intlname = new int();
		public int hier = new int();
		public int profit = 0;
		public int vacancyname = new int();
	}

	class Node
	{
		/// <summary>
		/// 点的标号
		/// </summary>
		public string name;
		/// <summary>
		/// 点的空间位置
		/// </summary>
		public string nodestation;
		/// <summary>
		/// 点的时间位置
		/// </summary>
		public int nodetime;
		/// <summary>
		/// 进弧的列表
		/// </summary>
		public List<Arc> inarc = new List<Arc>();
		/// <summary>
		/// 出弧的列表
		/// </summary>
		public List<Arc> outarc = new List<Arc>();
		/// <summary>
		/// 时空状态网中记载状态
		/// </summary>
		public int state;
		/// <summary>
		/// 标识
		/// </summary>
		public string mark;
		/// <summary>
		/// 拓扑标识
		/// </summary>
		public string topomark = "unvisited";
		/// <summary>
		/// 拓扑排序使用的标号
		/// </summary>
		public int order = -1;
		/// <summary>
		/// 用于拓扑排序前继节点
		/// </summary>
		public string before = "";
		/// <summary>
		/// 记录最短路径
		/// </summary>
		public ConcurrentDictionary<string, string> path = new ConcurrentDictionary<string, string>();
		/// <summary>
		/// 记录该节点的距离
		/// </summary>
		public ConcurrentDictionary<string, double> dis = new ConcurrentDictionary<string, double>();
	}

	class Demand
	{
		public int name;
		/// <summary>
		/// 货物的数量有多少
		/// </summary>
		public int amount;
		/// <summary>
		/// 成功运输对应的货物会获得多少报酬
		/// </summary>
		public int payment;
		/// <summary>
		/// 需求的起始点
		/// </summary>
		public string startstation;
		/// <summary>
		/// 起始时间
		/// </summary>
		public int starttime;
		/// <summary>
		/// 需求的终止点
		/// </summary>
		public string endstation;
		public int distance;
		/// <summary>
		/// 终止时间
		/// </summary>
		public int endtime;
		/// <summary>
		/// 路径
		/// </summary>
		public List<int> path_ids = new List<int>();
		/// <summary>
		/// 路径的绝对信息
		/// </summary>
		public HashSet<string> path_names = new HashSet<string>();
	}

	class Path
	{
		public int id = new int();

		public string name = "";

		public HashSet<string> included_arcs = new HashSet<string>();

		public double cost = 0.0;
	}

	class DataBatch()
	{
		public int ins;
		public int time;
		public double obj;
	}

	class Feature
	{
		public int node_num = new int();
		public int arc_num = new int();
		public int flow_num = new int();
	}

	class AggregateFlow
	{
		public int name = new int();
		public string origin = "";
		public int ori_amount = 0;
		public Dictionary<string, int> des_amount = new Dictionary<string, int>();
		public HashSet<int> including_commodities = new HashSet<int>();
	}
}
