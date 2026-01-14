// this code can correct output csv grid with given input files
#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include <unordered_map>
#include <algorithm>
#include <cctype>

using namespace std;

struct PowerNodeInfo {
    int net_id = 0;              // from Nets.asc: #xxx
    string net_name;             // from Nets.asc
    string nail_id = "0";        // from Nails.asc: $xxx, else "0"
    vector<string> pins;         // from Nets.asc pin list
};

struct KeywordConfig {
    vector<string> include_keys;
    vector<string> exclude_keys;
};

string trim_copy(const string& s) {
    size_t b = s.find_first_not_of(" \t\r\n");
    if (b == string::npos) return "";
    size_t e = s.find_last_not_of(" \t\r\n");
    return s.substr(b, e - b + 1);
}
//avoid special chars in csv
string csv_escape(const string& s) {
    bool need = false;
    for (char c : s) {
        if (c == ',' || c == '"' || c == '\n' || c == '\r') { need = true; break; }
    }
    if (!need) return s;

    string out = "\"";
    for (char c : s) out += (c == '"') ? "\"\"" : string(1, c);
    out += "\"";
    return out;
}

string to_lower_copy(string s) {
    for (char& c : s) c = (char)tolower(c);
    return s;// cost lots of memory
}

bool contains_ci(const string& s, const string& key) {
    if (key.empty()) return false;
    return (to_lower_copy(s).find(to_lower_copy(key)) != string::npos);
}

bool match_any_ci(const string& s, const vector<string>& keys) {
    for (const auto& k : keys) {
        if (contains_ci(s, k)) return true;
    }
    return false;
}

KeywordConfig load_keywords(const string& path) {
    KeywordConfig cfg;
    ifstream fin(path);
    if (!fin) {
        cerr << "ERROR: cannot open " << path << "\n";
        return cfg;
    }

    string line;
    while (getline(fin, line)) {
        line = trim_copy(line);
        if (line.size() >= 2 && (line[0] == '+' || line[0] == '-')) {
            string key = trim_copy(line.substr(1));
            if (key.empty()) {
                cout << "WARNING: invalid line in " << path << ": " << line << ". Process aborted." << "\n";
                break;
            }
            if (line[0] == '+') cfg.include_keys.push_back(key);
            else cfg.exclude_keys.push_back(key);// '-'
        }
        else {
            cout << "WARNING: invalid line in " << path << ": " << line << ". Process aborted." << "\n";
            break;
        }
    }
    return cfg;
}

// Parse Nets.asc: net_id, net_name, pins
unordered_map<int, PowerNodeInfo> parse_nets(const string& nets_path) {
    unordered_map<int, PowerNodeInfo> net_map;
    ifstream fin(nets_path);
    if (!fin) {
        cerr << "ERROR: cannot open nets path: " << nets_path << "\n";
        return net_map;
    }

    string line;
    int current_id = 0;

    while (getline(fin, line)) {
        string t = trim_copy(line);
        if (t.empty()) continue;

        if (!t.empty() && t[0] == '#') {
            // Header line: "#12    (S)  48V_VIN_EAST"
            istringstream iss(t);
            string netTok, typeTok;
            if (!(iss >> netTok >> typeTok)) {
                current_id = 0;
                continue;
            }

            if (netTok.size() < 2 || netTok[0] != '#') {
                current_id = 0;
                continue;
            }

            int id = 0;
            for (int i = 1; i < netTok.size(); i++) {
                if (!isdigit(netTok[i])) { id = 0; break; }
                id = id * 10 + (netTok[i] - '0');
            }
            if (id <= 0) {
                current_id = 0;
                continue;
            }

            string rest;
            getline(iss, rest);
            rest = trim_copy(rest);

            current_id = id;//NET ID
            auto& node = net_map[current_id];
            node.net_id = current_id;
            node.net_name = rest;
            continue;
        }

        // Pin line (belongs to current net)
        if (current_id != 0) {
            net_map[current_id].pins.push_back(t);
        }
    }

    return net_map;
}

// --- Parse Nails.asc: "$59 ... #12 48V_VIN_EAST ..." -> net_id -> nail_id
void attach_nails(const string& nails_path, unordered_map<int, PowerNodeInfo>& net_map) {
    ifstream fin(nails_path);
    if (!fin) {
        cerr << "ERROR: cannot open nails path: " << nails_path << "\n";
        return;
    }

    string line;
    while (getline(fin, line)) {
        if (line.empty() || line[0] != '$') continue;

        // $nail x y type grid (B) #netid netname...
        istringstream iss(line);
        string nail_id, grid, tb, netTok, netName;
        double x = 0, y = 0;
        int type = 0;

        if (!(iss >> nail_id >> x >> y >> type >> grid >> tb >> netTok >> netName))
            continue;

        if (netTok.size() < 2 || netTok[0] != '#') continue;

        int net_id = 0;
        for (int i = 1; i < netTok.size(); i++) {
            if (!isdigit(netTok[i])) { net_id = 0; break; }
            net_id = net_id * 10 + (netTok[i] - '0');
        }
        if (net_id <= 0) continue;

        auto it = net_map.find(net_id);
        if (it == net_map.end()) continue;
        it->second.nail_id = nail_id;//assign nail id
    }
}

void generate_power_node_list_csv(
    const string& nets_path,
    const string& nails_path,
    const string& cfg_path,
    const string& out_csv,
    bool only_with_nails
) {
    auto cfg = load_keywords(cfg_path);//load include/exclude keywords into struct

    auto net_map = parse_nets(nets_path);
    attach_nails(nails_path, net_map);

    // Filter and collect rows
    vector<PowerNodeInfo> rows;
    rows.reserve(net_map.size());

    for (auto& kv : net_map) {
        auto& node = kv.second;

        // exclude
        if (match_any_ci(node.net_name, cfg.exclude_keys)) continue;

        // include
        if (!match_any_ci(node.net_name, cfg.include_keys)) continue;


        // optional: only keep nets that have nails (nail_id != 0)
        if (only_with_nails) {
            if (node.nail_id.empty() || node.nail_id == "0") continue;
        }
        rows.push_back(node);
    }
    sort(rows.begin(), rows.end(), [](const PowerNodeInfo& a, const PowerNodeInfo& b) {
        return a.net_id < b.net_id;
        });

    ofstream fout(out_csv);
    if (!fout) {
        cerr << "ERROR: cannot write " << out_csv << "\n";
        return;
    }

    // csv
    fout << "net_id,net_name,nail_id,related_pins_cnt,related_pins_list\n";

    for (const auto& n : rows) {
        string pinsJoined;
        for (int i = 0; i < n.pins.size(); i++) {
            if (i)
                pinsJoined += ", ";
            pinsJoined += n.pins[i];
        }
        int nail_id_int = 0;
        if (!n.nail_id.empty() && n.nail_id[0] == '$')
            nail_id_int = stoi(n.nail_id.substr(1));

        fout << n.net_id << ","
            << csv_escape(n.net_name) << ","
            << nail_id_int << ","
            << n.pins.size() << ","
            << csv_escape(pinsJoined) << "\n";
    }

    cout << "Power nodes found: " << rows.size() << "\n";
    cout << "Output: " << out_csv << "\n";
}

static void print_usage() {
    cerr << "Usage:\n"
        << "  PowerNodeGen.exe --nets <Nets.asc> --nails <Nails.asc> --cfg <power_keywords.txt> --out <PowerNodeList.csv> [--only-nails]\n"
        << "Example:\n"
        << "  PowerNodeGen.exe --nets \"C:\\\\path\\\\Nets.asc\" --nails \"C:\\\\path\\\\Nails.asc\" --cfg \"C:\\\\path\\\\cfg.txt\" --out \"C:\\\\out\\\\PowerNodeList.csv\"\n";
}

static bool get_arg(int argc, char* argv[], const string& key, string& out) {
    for (int i = 1; i < argc; i++) {
        if (string(argv[i]) == key) {
            if (i + 1 >= argc) return false;
            out = argv[i + 1];
            return true;
        }
    }
    return false;
}

static bool has_flag(int argc, char* argv[], const string& key) {
    for (int i = 1; i < argc; i++) {
        if (string(argv[i]) == key) return true;
    }
    return false;
}

int main(int argc, char* argv[]) {
    // default values (backward compatible)
    string nets = "Nets.asc";
    string nails = "Nails.asc";
    string cfg = "power_keywords.txt";
    string out = "PowerNodeList.csv";

    // parse args
    string val;
    if (get_arg(argc, argv, "--nets", val))  nets = val;
    if (get_arg(argc, argv, "--nails", val)) nails = val;
    if (get_arg(argc, argv, "--cfg", v// this code can correct output csv grid with given input files
#include <iostream>
#include <fstream>
#include <sstream>
#include <string>
#include <vector>
#include <unordered_map>
#include <algorithm>
#include <cctype>

        using namespace std;

    struct PowerNodeInfo {
        int net_id = 0;              // from Nets.asc: #xxx
        string net_name;             // from Nets.asc
        string nail_id = "0";        // from Nails.asc: $xxx, else "0"
        vector<string> pins;         // from Nets.asc pin list
    };

    struct KeywordConfig {
        vector<string> include_keys;
        vector<string> exclude_keys;
    };

    string trim_copy(const string & s) {
        size_t b = s.find_first_not_of(" \t\r\n");
        if (b == string::npos) return "";
        size_t e = s.find_last_not_of(" \t\r\n");
        return s.substr(b, e - b + 1);
    }
    //avoid special chars in csv
    string csv_escape(const string & s) {
        bool need = false;
        for (char c : s) {
            if (c == ',' || c == '"' || c == '\n' || c == '\r') { need = true; break; }
        }
        if (!need) return s;

        string out = "\"";
        for (char c : s) out += (c == '"') ? "\"\"" : string(1, c);
        out += "\"";
        return out;
    }

    string to_lower_copy(string s) {
        for (char& c : s) c = (char)tolower(c);
        return s;// cost lots of memory
    }

    bool contains_ci(const string & s, const string & key) {
        if (key.empty()) return false;
        return (to_lower_copy(s).find(to_lower_copy(key)) != string::npos);
    }

    bool match_any_ci(const string & s, const vector<string>&keys) {
        for (const auto& k : keys) {
            if (contains_ci(s, k)) return true;
        }
        return false;
    }

    KeywordConfig load_keywords(const string & path) {
        KeywordConfig cfg;
        ifstream fin(path);
        if (!fin) {
            cerr << "ERROR: cannot open " << path << "\n";
            return cfg;
        }

        string line;
        while (getline(fin, line)) {
            line = trim_copy(line);
            if (line.size() >= 2 && (line[0] == '+' || line[0] == '-')) {
                string key = trim_copy(line.substr(1));
                if (key.empty()) {
                    cout << "WARNING: invalid line in " << path << ": " << line << ". Process aborted." << "\n";
                    break;
                }
                if (line[0] == '+') cfg.include_keys.push_back(key);
                else cfg.exclude_keys.push_back(key);// '-'
            }
            else {
                cout << "WARNING: invalid line in " << path << ": " << line << ". Process aborted." << "\n";
                break;
            }
        }
        return cfg;
    }

    // Parse Nets.asc: net_id, net_name, pins
    unordered_map<int, PowerNodeInfo> parse_nets(const string & nets_path) {
        unordered_map<int, PowerNodeInfo> net_map;
        ifstream fin(nets_path);
        if (!fin) {
            cerr << "ERROR: cannot open nets path: " << nets_path << "\n";
            return net_map;
        }

        string line;
        int current_id = 0;

        while (getline(fin, line)) {
            string t = trim_copy(line);
            if (t.empty()) continue;

            if (!t.empty() && t[0] == '#') {
                // Header line: "#12    (S)  48V_VIN_EAST"
                istringstream iss(t);
                string netTok, typeTok;
                if (!(iss >> netTok >> typeTok)) {
                    current_id = 0;
                    continue;
                }

                if (netTok.size() < 2 || netTok[0] != '#') {
                    current_id = 0;
                    continue;
                }

                int id = 0;
                for (int i = 1; i < netTok.size(); i++) {
                    if (!isdigit(netTok[i])) { id = 0; break; }
                    id = id * 10 + (netTok[i] - '0');
                }
                if (id <= 0) {
                    current_id = 0;
                    continue;
                }

                string rest;
                getline(iss, rest);
                rest = trim_copy(rest);

                current_id = id;//NET ID
                auto& node = net_map[current_id];
                node.net_id = current_id;
                node.net_name = rest;
                continue;
            }

            // Pin line (belongs to current net)
            if (current_id != 0) {
                net_map[current_id].pins.push_back(t);
            }
        }

        return net_map;
    }

    // --- Parse Nails.asc: "$59 ... #12 48V_VIN_EAST ..." -> net_id -> nail_id
    void attach_nails(const string & nails_path, unordered_map<int, PowerNodeInfo>&net_map) {
        ifstream fin(nails_path);
        if (!fin) {
            cerr << "ERROR: cannot open nails path: " << nails_path << "\n";
            return;
        }

        string line;
        while (getline(fin, line)) {
            if (line.empty() || line[0] != '$') continue;

            // $nail x y type grid (B) #netid netname...
            istringstream iss(line);
            string nail_id, grid, tb, netTok, netName;
            double x = 0, y = 0;
            int type = 0;

            if (!(iss >> nail_id >> x >> y >> type >> grid >> tb >> netTok >> netName))
                continue;

            if (netTok.size() < 2 || netTok[0] != '#') continue;

            int net_id = 0;
                for (int i = 1; i < netTok.size(); i++) {
                if (!isdigit(netTok[i])) { net_id = 0; break; }
                net_id = net_id * 10 + (netTok[i] - '0');
            }
            if (net_id <= 0) continue;

            auto it = net_map.find(net_id);
            if (it == net_map.end()) continue;
            it->second.nail_id = nail_id;//assign nail id
        }
    }

    void generate_power_node_list_csv(
        const string & nets_path,
        const string & nails_path,
        const string & cfg_path,
        const string & out_csv,
        bool only_with_nails
    ) {
        auto cfg = load_keywords(cfg_path);//load include/exclude keywords into struct

        auto net_map = parse_nets(nets_path);
        attach_nails(nails_path, net_map);

        // Filter and collect rows
        vector<PowerNodeInfo> rows;
        rows.reserve(net_map.size());

        for (auto& kv : net_map) {
            auto& node = kv.second;

            // exclude
            if (match_any_ci(node.net_name, cfg.exclude_keys)) continue;

            // include
            if (!match_any_ci(node.net_name, cfg.include_keys)) continue;


            // optional: only keep nets that have nails (nail_id != 0)
            if (only_with_nails) {
                if (node.nail_id.empty() || node.nail_id == "0") continue;
            }
            rows.push_back(node);
        }
        sort(rows.begin(), rows.end(), [](const PowerNodeInfo& a, const PowerNodeInfo& b) {
            return a.net_id < b.net_id;
            });

        ofstream fout(out_csv);
        if (!fout) {
            cerr << "ERROR: cannot write " << out_csv << "\n";
            return;
        }

        // csv
        fout << "net_id,net_name,nail_id,related_pins_cnt,related_pins_list\n";

        for (const auto& n : rows) {
            string pinsJoined;
            for (int i = 0; i < n.pins.size(); i++) {
                if (i)
                    pinsJoined += ", ";
                pinsJoined += n.pins[i];
            }
            int nail_id_int = 0;
            if (!n.nail_id.empty() && n.nail_id[0] == '$')
                nail_id_int = stoi(n.nail_id.substr(1));

            fout << n.net_id << ","
                << csv_escape(n.net_name) << ","
                << nail_id_int << ","
                << n.pins.size() << ","
                << csv_escape(pinsJoined) << "\n";
        }

        cout << "Power nodes found: " << rows.size() << "\n";
        cout << "Output: " << out_csv << "\n";
    }

    static void print_usage() {
        cerr << "Usage:\n"
            << "  PowerNodeGen.exe --nets <Nets.asc> --nails <Nails.asc> --cfg <power_keywords.txt> --out <PowerNodeList.csv> [--only-nails]\n"
            << "Example:\n"
            << "  PowerNodeGen.exe --nets \"C:\\\\path\\\\Nets.asc\" --nails \"C:\\\\path\\\\Nails.asc\" --cfg \"C:\\\\path\\\\cfg.txt\" --out \"C:\\\\out\\\\PowerNodeList.csv\"\n";
    }

    static bool get_arg(int argc, char* argv[], const string & key, string & out) {
        for (int i = 1; i < argc; i++) {
            if (string(argv[i]) == key) {
                if (i + 1 >= argc) return false;
                out = argv[i + 1];
                return true;
            }
        }
        return false;
    }

    static bool has_flag(int argc, char* argv[], const string & key) {
        for (int i = 1; i < argc; i++) {
            if (string(argv[i]) == key) return true;
        }
        return false;
    }

    int main(int argc, char* argv[]) {
        // default values (backward compatible)
        string nets = "Nets.asc";
        string nails = "Nails.asc";
        string cfg = "power_keywords.txt";
        string out = "PowerNodeList.csv";

        // parse args
        string val;
        if (get_arg(argc, argv, "--nets", val))  nets = val;
        if (get_arg(argc, argv, "--nails", val)) nails = val;
        if (get_arg(argc, argv, "--cfg", val))   cfg = val;
        if (get_arg(argc, argv, "--out", val))   out = val;

        bool only_with_nails = has_flag(argc, argv, "--only-nails") || has_flag(argc, argv, "--onlyNails");

        // If user passed any args but forgot value for a flag, show usage
        // (simple check: if they typed a flag as the last token)
        for (int i = 1; i < argc; i++) {
            string a = argv[i];
            if ((a == "--nets" || a == "--nails" || a == "--cfg" || a == "--out") && i + 1 >= argc) {
                print_usage();
                return 2;
            }
        }

        generate_power_node_list_csv(nets, nails, cfg, out, only_with_nails);
        return 0;
    }
    al))   cfg = val;
    if (get_arg(argc, argv, "--out", val))   out = val;

    bool only_with_nails = has_flag(argc, argv, "--only-nails") || has_flag(argc, argv, "--onlyNails");

    // If user passed any args but forgot value for a flag, show usage
    // (simple check: if they typed a flag as the last token)
    for (int i = 1; i < argc; i++) {
        string a = argv[i];
        if ((a == "--nets" || a == "--nails" || a == "--cfg" || a == "--out") && i + 1 >= argc) {
            print_usage();
            return 2;
        }
    }

    generate_power_node_list_csv(nets, nails, cfg, out, only_with_nails);
    return 0;
}
