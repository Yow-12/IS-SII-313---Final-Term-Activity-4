using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;

namespace Activity3
{
    public partial class Dashboard : Form
    {
        private int selectedInventoryId = 0;

        public Dashboard()
        {
            InitializeComponent();

            button16.Click += AddButton_Click;
            button7.Click += UpdateButton_Click;
            button8.Click += DeleteButton_Click;
            button9.Click += ClearButton_Click;
            button10.Click += SearchButton_Click;
            button11.Click += RefreshButton_Click;
            dataGridView1.CellClick += DataGridView1_CellClick;
            textBox6.KeyDown += SearchTextBox_KeyDown;
            Shown += Dashboard_Shown;
        }

        private void Dashboard_Shown(object? sender, EventArgs e)
        {
            try
            {
                Database.Initialize();
                SetupInventoryGrid();
                LoadInventory();
                LoadCategories();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to load the inventory database:\n\n" + ex.Message,
                    "Database error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupInventoryGrid()
        {
            dataGridView1.AutoGenerateColumns = true;
            dataGridView1.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.MultiSelect = false;
            dataGridView1.ReadOnly = true;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        }

        private void LoadCategories()
        {
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(new object[]
            {
                "Computer Parts", "Accessories", "Office Supplies", "Equipment", "Other"
            });
        }

        private void LoadInventory(string search = "")
        {
            DataTable table = Database.GetInventory(search);
            inventoryBindingSource.DataSource = table;

            if (dataGridView1.Columns.Contains("ID"))
            {
                dataGridView1.Columns["ID"].Visible = true;
                dataGridView1.Columns["ID"].HeaderText = "Product ID";
                dataGridView1.Columns["ID"].DisplayIndex = 0;
            }
            if (dataGridView1.Columns.Contains("CreatedAt"))
            {
                dataGridView1.Columns["CreatedAt"].HeaderText = "Created At";
                dataGridView1.Columns["CreatedAt"].DefaultCellStyle.Format = "yyyy-MM-dd HH:mm";
                dataGridView1.Columns["CreatedAt"].DisplayIndex = dataGridView1.Columns.Count - 1;
            }
            if (dataGridView1.Columns.Contains("UnitPrice"))
                dataGridView1.Columns["UnitPrice"].HeaderText = "Unit Price";
            if (dataGridView1.Columns.Contains("ProductName"))
                dataGridView1.Columns["ProductName"].HeaderText = "Product Name";
            if (dataGridView1.Columns.Contains("ReorderLevel"))
                dataGridView1.Columns["ReorderLevel"].HeaderText = "Reorder Level";

            UpdateDashboardCounts();
            selectedInventoryId = 0;
        }

        private void UpdateDashboardCounts()
        {
            Database.GetInventoryCounts(out int total, out int inStock, out int lowStock, out int outOfStock);
            label16.Text = total.ToString();
            label17.Text = inStock.ToString();
            label24.Text = lowStock.ToString();
            label31.Text = outOfStock.ToString();
        }

        private bool ReadForm(out string productName, out string category, out decimal unitPrice,
            out string description, out int quantity, out int reorderLevel)
        {
            productName = textBox1.Text.Trim();
            category = comboBox1.Text.Trim();
            description = textBox3.Text.Trim();
            unitPrice = 0;
            quantity = 0;
            reorderLevel = 0;

            if (string.IsNullOrWhiteSpace(productName))
            {
                MessageBox.Show("Please enter the product name.", "Required field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox1.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                MessageBox.Show("Please select a category.", "Required field", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                comboBox1.Focus();
                return false;
            }

            if (!decimal.TryParse(textBox2.Text.Trim(), out unitPrice) || unitPrice < 0)
            {
                MessageBox.Show("Please enter a valid unit price.", "Invalid price", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox2.Focus();
                return false;
            }

            if (!int.TryParse(textBox4.Text.Trim(), out quantity) || quantity < 0)
            {
                MessageBox.Show("Please enter a valid quantity (0 or higher).", "Invalid quantity", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox4.Focus();
                return false;
            }

            if (!int.TryParse(textBox5.Text.Trim(), out reorderLevel) || reorderLevel < 0)
            {
                MessageBox.Show("Please enter a valid reorder level (0 or higher).", "Invalid reorder level", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                textBox5.Focus();
                return false;
            }

            return true;
        }

        private void AddButton_Click(object? sender, EventArgs e)
        {
            if (!ReadForm(out string productName, out string category, out decimal unitPrice,
                out string description, out int quantity, out int reorderLevel)) return;

            try
            {
                Database.AddInventory(productName, category, unitPrice, description, quantity, reorderLevel);
                MessageBox.Show("Product added successfully.", "Add Product", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearForm();
                LoadInventory();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not add the product:\n\n" + ex.Message, "Database error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateButton_Click(object? sender, EventArgs e)
        {
            if (selectedInventoryId == 0)
            {
                MessageBox.Show("Select a product from the table first.", "Update Product", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!ReadForm(out string productName, out string category, out decimal unitPrice,
                out string description, out int quantity, out int reorderLevel)) return;

            try
            {
                Database.UpdateInventory(selectedInventoryId, productName, category, unitPrice,
                    description, quantity, reorderLevel);
                MessageBox.Show("Product updated successfully.", "Update Product", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearForm();
                LoadInventory();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update the product:\n\n" + ex.Message, "Database error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteButton_Click(object? sender, EventArgs e)
        {
            if (selectedInventoryId == 0)
            {
                MessageBox.Show("Select a product from the table first.", "Delete Product", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            DialogResult result = MessageBox.Show("Delete the selected product?", "Confirm Delete",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            try
            {
                Database.DeleteInventory(selectedInventoryId);
                MessageBox.Show("Product deleted successfully.", "Delete Product", MessageBoxButtons.OK, MessageBoxIcon.Information);
                ClearForm();
                LoadInventory();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not delete the product:\n\n" + ex.Message, "Database error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ClearButton_Click(object? sender, EventArgs e) => ClearForm();

        private void ClearForm()
        {
            selectedInventoryId = 0;
            textBox1.Clear();
            comboBox1.SelectedIndex = -1;
            textBox2.Clear();
            textBox3.Clear();
            textBox4.Clear();
            textBox5.Clear();
            dataGridView1.ClearSelection();
            textBox1.Focus();
        }

        private void DataGridView1_CellClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            DataGridViewRow row = dataGridView1.Rows[e.RowIndex];

            selectedInventoryId = Convert.ToInt32(row.Cells["ID"].Value);
            textBox1.Text = Convert.ToString(row.Cells["ProductName"].Value) ?? "";
            comboBox1.Text = Convert.ToString(row.Cells["Category"].Value) ?? "";
            textBox2.Text = Convert.ToDecimal(row.Cells["UnitPrice"].Value).ToString("0.00");
            textBox3.Text = Convert.ToString(row.Cells["Description"].Value) ?? "";
            textBox4.Text = Convert.ToString(row.Cells["Quantity"].Value) ?? "0";
            textBox5.Text = Convert.ToString(row.Cells["ReorderLevel"].Value) ?? "0";
        }

        private void SearchButton_Click(object? sender, EventArgs e) => LoadInventory(textBox6.Text.Trim());

        private void RefreshButton_Click(object? sender, EventArgs e)
        {
            textBox6.Clear();
            ClearForm();
            LoadInventory();
        }

        private void SearchTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                SearchButton_Click(sender, EventArgs.Empty);
                e.SuppressKeyPress = true;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }

        private void button6_Click(object sender, EventArgs e)
        {
            using var Login = new Login();
            Hide();
            Login.ShowDialog(this);
            Show();
        }

        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) { }
    }
}
