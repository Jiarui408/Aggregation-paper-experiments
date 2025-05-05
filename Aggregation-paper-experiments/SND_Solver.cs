using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using Gurobi;
using static System.Windows.Forms.LinkLabel;

namespace Aggregation_paper_experiments
{
	class SND_Solver
	{
		/// <summary>
		/// GUROBI聚合求解
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_aggregate_solve(SND_DataStructure ds)
		{
			GRBEnv env = new GRBEnv("Aggregate_SND.log");
			int TimeLimit = ds.time;
			double OptimalGapLimit = ds.gap;
			env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
			env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
			GRBModel Model = new GRBModel(env);

			Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
			Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
			Dictionary<string, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<string, Dictionary<string, GRBLinExpr>>();
			Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

			BuildGRBModel();
			Model.Optimize();
			int status = Model.Get(GRB.IntAttr.Status);
			int solution = Model.Get(GRB.IntAttr.SolCount);

			//可行则结果细化
			if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
			{
				ds.aggregation_solving_time = Model.Runtime;
				ds.aggregation_gap = Model.MIPGap;
				Console.WriteLine(Model.Runtime);
				foreach (var v in Xak)
				{
					if (v.Value.X > 0.0)
					{
						string[] s1 = v.Key.Split('*');
						string agg_flowname = s1[0].ToString();
						string arcname = s1[1].ToString();
						if (ds.aggregateflow_arc_quanity.ContainsKey(agg_flowname) == false)
							ds.aggregateflow_arc_quanity[agg_flowname] = new Dictionary<string, int>();
						ds.aggregateflow_arc_quanity[agg_flowname][arcname] = Convert.ToInt32(v.Value.X);
					}
				}

				Model.Dispose();
				env.Dispose();
			}
			//不可行
			else
			{
				Model.ComputeIIS();
				foreach (GRBConstr c in Model.GetConstrs())
				{
					if (Convert.ToBoolean(c.IISConstr))
						Console.WriteLine(c.ConstrName);
				}
				Model.Dispose();
				env.Dispose();
				Console.WriteLine("出现问题！");
			}

			/// <summary>
			/// 搭建模型
			/// </summary>
			void BuildGRBModel()
			{
				//决策变量
				BuildDecisionVar();
				//约束条件
				BuildConstraint();
			}
			/// <summary>
			/// 构建决策变量及目标函数
			/// </summary>
			void BuildDecisionVar()
			{
				//货流变量
				foreach (Arc a in ds.arcs.Values)
				{
					foreach (AggregateFlow af in ds.aggregate_flows.Values)
					{
						string s = af.origin + "*" + a.name;
						Xak.Add(s, Model.AddVar(0.0, af.ori_amount, a.length * ds.demand_arc_cost_perlength, GRB.INTEGER, s));
						PrepareConstraintForAggregate(af, a, Xak[s], "aggregate");
					}

					if (Ya.ContainsKey(a.name) == false)
					{
						string s1 = a.name;
						Ya.Add(s1, Model.AddVar(0.0, ds.max_service_frequency, a.length * ds.service_cost_perlength, GRB.INTEGER, s1));
						Demand d = new Demand();
						PrepareConstraint(d, a, Ya[s1], "Ya");
					}

				}
				Model.Set(GRB.IntAttr.ModelSense, 1);
			}

			/// <summary>
			/// 构建约束条件
			/// </summary>
			void BuildConstraint()
			{
				//约束1：流平衡约束
				BuildBalanceFlowConstraint();
				//约束2：耦合约束
				BuildBundleConstraint();
			}

			/// <summary>
			/// 建立耦合约束
			/// </summary>
			void BuildBundleConstraint()
			{
				foreach (var v in arc_capacity)
				{
					Model.AddConstr(v.Value >= 0, v.Key);
				}
			}

			/// <summary>
			/// 建立流平衡约束
			/// </summary>
			void BuildBalanceFlowConstraint()
			{
				//货流平衡
				foreach (AggregateFlow af in ds.aggregate_flows.Values)
				{
					Dictionary<string, int> endnode_amount = new Dictionary<string, int>();
					foreach (var v in af.des_amount)
					{
						endnode_amount[v.Key] = v.Value;
					}
					foreach (var v in demand_node_balance[af.origin])
					{
						if (v.Key == af.origin)
						{
							Model.AddConstr(v.Value == -af.ori_amount, af.origin + "-" + v.Key);
							continue;
						}
						if (endnode_amount.ContainsKey(v.Key))
						{
							Model.AddConstr(v.Value == endnode_amount[v.Key], af.origin + "-" + v.Key);
							continue;
						}
						if (v.Key != af.origin && endnode_amount.ContainsKey(v.Key) == false)
						{
							Model.AddConstr(v.Value == 0, af.origin + "-" + v.Key);
							continue;
						}
					}
				}
			}

			void PrepareConstraint(Demand d, Arc a, GRBVar var, string GRBVar_name)
			{
				if (GRBVar_name == "Ya")
				{
					if (arc_capacity.ContainsKey(a.name) == false)
						arc_capacity[a.name] = new GRBLinExpr();
					arc_capacity[a.name] = arc_capacity[a.name] + var * ds.service_capacity;
				}
			}

			void PrepareConstraintForAggregate(AggregateFlow af, Arc a, GRBVar var, string GRBVar_name)
			{
				if (GRBVar_name == "aggregate")
				{
					string afname = af.origin;
					//流平衡约束
					if (demand_node_balance.ContainsKey(afname) == false)
						demand_node_balance[afname] = new Dictionary<string, GRBLinExpr>();
					if (demand_node_balance[afname].ContainsKey(a.outnode.name) == false)
						demand_node_balance[afname][a.outnode.name] = new GRBLinExpr();
					if (demand_node_balance[afname].ContainsKey(a.innode.name) == false)
						demand_node_balance[afname][a.innode.name] = new GRBLinExpr();
					demand_node_balance[afname][a.outnode.name] = demand_node_balance[afname][a.outnode.name] - var;
					demand_node_balance[afname][a.innode.name] = demand_node_balance[afname][a.innode.name] + var;

					if (arc_capacity.ContainsKey(a.name) == false)
						arc_capacity[a.name] = new GRBLinExpr();
					arc_capacity[a.name] = arc_capacity[a.name] - var;
				}
			}
		}

		/// <summary>
		/// GUROBI解聚
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_disaggregate_solve(SND_DataStructure ds)
		{
			foreach (var aggregateflow in ds.aggregateflow_arc_quanity)
			{
				string aggregateflow_name = aggregateflow.Key;
				Dictionary<string, int> arc_quantity = new Dictionary<string, int>();
				arc_quantity = aggregateflow.Value;

				Disaggregate_each_flow(aggregateflow_name, arc_quantity);
			}

			void Disaggregate_each_flow(string aggregateflow_name, Dictionary<string, int> arc_quantity)
			{
				GRBEnv env = new GRBEnv("Disaggregate_SND.log");
				env.Set(GRB.IntParam.LogToConsole, 0);
				int TimeLimit = ds.time;
				double OptimalGapLimit = ds.gap;
				env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
				env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
				GRBModel Model = new GRBModel(env);

				//env.Set(GRB.IntParam.Method, 1);

				Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
				Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
				Dictionary<int, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<int, Dictionary<string, GRBLinExpr>>();
				Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

				BuildGRBModel();
				Model.Optimize();
				int status = Model.Get(GRB.IntAttr.Status);
				int solution = Model.Get(GRB.IntAttr.SolCount);

				//可行则结果细化
				if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
				{
					Model.Update();
					ds.disaggregation_solving_time = ds.disaggregation_solving_time + Model.Runtime;


					Model.Dispose();
					env.Dispose();
				}
				//不可行
				else
				{
					Model.ComputeIIS();
					foreach (GRBConstr c in Model.GetConstrs())
					{
						if (Convert.ToBoolean(c.IISConstr))
							Console.WriteLine(c.ConstrName);
					}
					Model.Dispose();
					env.Dispose();
					Console.WriteLine("出现问题！");
				}

				/// <summary>
				/// 搭建模型
				/// </summary>
				void BuildGRBModel()
				{
					//决策变量
					BuildDecisionVar();
					//约束条件
					BuildConstraint();
				}
				/// <summary>
				/// 构建决策变量及目标函数
				/// </summary>
				void BuildDecisionVar()
				{
					foreach (string arc_name in arc_quantity.Keys)
					{
						Arc a = ds.arcs[arc_name];
						GRBLinExpr lin = 0;
						foreach (int de_name in ds.aggregate_flows[aggregateflow_name].including_commodities)
						{
							string s = de_name + "*" + arc_name;
							Xak.Add(s, Model.AddVar(0.0, ds.demands[de_name].amount, 0.0, GRB.INTEGER, s));
							PrepareConstraint(ds.demands[de_name], a, Xak[s], "Xak");
							lin = lin + Xak[s];
						}
						Model.AddConstr(lin == arc_quantity[arc_name], arc_name);
					}

				}

				/// <summary>
				/// 构建约束条件
				/// </summary>
				void BuildConstraint()
				{
					//约束1：流平衡约束
					BuildBalanceFlowConstraint();
				}

				/// <summary>
				/// 建立流平衡约束
				/// </summary>
				void BuildBalanceFlowConstraint()
				{
					foreach (int de_name in ds.aggregate_flows[aggregateflow_name].including_commodities)
					{
						Demand de = ds.demands[de_name];
						Node startnode = ds.nodes[de.startstation];
						Node endnode = ds.nodes[de.endstation];
						foreach (var v in demand_node_balance[de.name])
						{
							if (v.Key == startnode.name)
							{
								Model.AddConstr(v.Value == -de.amount, de.name + "-" + v.Key);
								continue;
							}
							if (v.Key == endnode.name)
							{
								Model.AddConstr(v.Value == de.amount, de.name + "-" + v.Key);
								continue;
							}
							if (v.Key != startnode.name && v.Key != endnode.name)
							{
								Model.AddConstr(v.Value == 0, de.name + "-" + v.Key);
								continue;
							}
						}
					}
				}


				void PrepareConstraint(Demand d, Arc a, GRBVar var, string GRBVar_name)
				{
					if (GRBVar_name == "Xak")
					{
						//流平衡约束
						if (demand_node_balance.ContainsKey(d.name) == false)
							demand_node_balance[d.name] = new Dictionary<string, GRBLinExpr>();
						if (demand_node_balance[d.name].ContainsKey(a.outnode.name) == false)
							demand_node_balance[d.name][a.outnode.name] = new GRBLinExpr();
						if (demand_node_balance[d.name].ContainsKey(a.innode.name) == false)
							demand_node_balance[d.name][a.innode.name] = new GRBLinExpr();
						demand_node_balance[d.name][a.outnode.name] = demand_node_balance[d.name][a.outnode.name] - var;
						demand_node_balance[d.name][a.innode.name] = demand_node_balance[d.name][a.innode.name] + var;
					}
				}
			}
		}

		/// <summary>
		/// GUROBI分着求解
		/// </summary>
		/// <param name="ds"></param>
		public void Gurobi_single_solve(SND_DataStructure ds)
		{
			GRBEnv env = new GRBEnv("Original_SND.log");
			int TimeLimit = ds.time;
			double OptimalGapLimit = ds.gap;
			env.Set(GRB.DoubleParam.MIPGap, OptimalGapLimit);//mipgap是可行解与目标之间的距离
			env.Set(GRB.DoubleParam.TimeLimit, TimeLimit);
			GRBModel Model = new GRBModel(env);

			Dictionary<string, GRBVar> Xak = new Dictionary<string, GRBVar>();
			Dictionary<string, GRBVar> Ya = new Dictionary<string, GRBVar>();
			Dictionary<int, Dictionary<string, GRBLinExpr>> demand_node_balance = new Dictionary<int, Dictionary<string, GRBLinExpr>>();
			Dictionary<string, GRBLinExpr> arc_capacity = new Dictionary<string, GRBLinExpr>();

			BuildGRBModel();
			Model.Optimize();
			int status = Model.Get(GRB.IntAttr.Status);
			int solution = Model.Get(GRB.IntAttr.SolCount);

			//可行则结果细化
			if (status == GRB.Status.OPTIMAL || (status == GRB.Status.TIME_LIMIT))
			{
				ds.original_solving_time = Model.Runtime;
				ds.original_gap = Model.MIPGap;
				Model.Dispose();
				env.Dispose();
			}
			//不可行
			else
			{
				Model.ComputeIIS();
				foreach (GRBConstr c in Model.GetConstrs())
				{
					if (Convert.ToBoolean(c.IISConstr))
						Console.WriteLine(c.ConstrName);
				}
				Model.Dispose();
				env.Dispose();
				Console.WriteLine("出现问题！");
			}

			/// <summary>
			/// 搭建模型
			/// </summary>
			void BuildGRBModel()
			{
				//决策变量
				BuildDecisionVar();
				//约束条件
				BuildConstraint();
			}
			/// <summary>
			/// 构建决策变量及目标函数
			/// </summary>
			void BuildDecisionVar()
			{
				GRBLinExpr obj = new GRBLinExpr();
				//货流变量
				foreach (Arc a in ds.arcs.Values)
				{
					foreach (Demand de in ds.demands.Values)
					{
						string s = de.name + "*" + a.name;
						Xak.Add(s, Model.AddVar(0.0, de.amount, a.length * ds.demand_arc_cost_perlength, GRB.INTEGER, s));
						PrepareConstraint(de, a, Xak[s], "Xak");
						if (Ya.ContainsKey(a.name) == false)
						{
							string s1 = a.name;
							Ya.Add(s1, Model.AddVar(0.0, ds.max_service_frequency, a.length * ds.service_cost_perlength, GRB.INTEGER, s1));
							Demand d = new Demand();
							PrepareConstraint(d, a, Ya[s1], "Ya");
						}
					}
				}
				Model.Set(GRB.IntAttr.ModelSense, 1);
			}

			/// <summary>
			/// 构建约束条件
			/// </summary>
			void BuildConstraint()
			{
				//约束1：流平衡约束
				BuildBalanceFlowConstraint();
				//约束2：耦合约束
				BuildBundleConstraint();
			}

			/// <summary>
			/// 建立耦合约束
			/// </summary>
			void BuildBundleConstraint()
			{
				foreach (var v in arc_capacity)
				{
					Model.AddConstr(v.Value >= 0, v.Key);
				}
			}

			/// <summary>
			/// 建立流平衡约束
			/// </summary>
			void BuildBalanceFlowConstraint()
			{
				//货流平衡
				foreach (Demand de in ds.demands.Values)
				{
					Node startnode = ds.nodes[de.startstation];
					Node endnode = ds.nodes[de.endstation];
					foreach (var v in demand_node_balance[de.name])
					{
						if (v.Key == startnode.name)
						{
							Model.AddConstr(v.Value == -de.amount, de.name + "-" + v.Key);
							continue;
						}
						if (v.Key == endnode.name)
						{
							Model.AddConstr(v.Value == de.amount, de.name + "-" + v.Key);
							continue;
						}
						if (v.Key != startnode.name && v.Key != endnode.name)
						{
							Model.AddConstr(v.Value == 0, de.name + "-" + v.Key);
							continue;
						}
					}
				}
			}


			void PrepareConstraint(Demand d, Arc a, GRBVar var, string GRBVar_name)
			{
				if (GRBVar_name == "Xak")
				{
					//流平衡约束
					if (demand_node_balance.ContainsKey(d.name) == false)
						demand_node_balance[d.name] = new Dictionary<string, GRBLinExpr>();
					if (demand_node_balance[d.name].ContainsKey(a.outnode.name) == false)
						demand_node_balance[d.name][a.outnode.name] = new GRBLinExpr();
					if (demand_node_balance[d.name].ContainsKey(a.innode.name) == false)
						demand_node_balance[d.name][a.innode.name] = new GRBLinExpr();
					demand_node_balance[d.name][a.outnode.name] = demand_node_balance[d.name][a.outnode.name] - var;
					demand_node_balance[d.name][a.innode.name] = demand_node_balance[d.name][a.innode.name] + var;


					if (arc_capacity.ContainsKey(a.name) == false)
						arc_capacity[a.name] = new GRBLinExpr();
					arc_capacity[a.name] = arc_capacity[a.name] - var;

				}
				if (GRBVar_name == "Ya")
				{
					if (arc_capacity.ContainsKey(a.name) == false)
						arc_capacity[a.name] = new GRBLinExpr();
					arc_capacity[a.name] = arc_capacity[a.name] + var * ds.service_capacity;
				}
			}
		}
	}
}
