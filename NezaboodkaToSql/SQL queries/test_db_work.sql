/*********************************************

	Create Test Nezaboodka database structure
        
**********************************************/

use `nz_admin_db`;

insert into `db_rem_list`
values ('nz_test_db');

insert into `db_add_list`
values ('nz_test_db');

CALL alter_database_list();	# db removal before inserting


/*  Fill test database schema */

USE `nz_test_db`;

insert into `type`
(`name`, `table_name`, `base_type_name`)
values
('User', 'user_table', ''),
('Admin', 'admin_table', 'User'),
('GoodAdmin', 'good_admin_table', 'Admin'),
('Group', 'group_table', '');


insert into `field`
(`name`, `col_name`, `owner_type_name`, `type_name`, `compare_options`, `is_list`, `back_ref_name`)
values
('Name', 'name', 'User', 'VARCHAR(60)', 'IgnoreCase', FALSE, NULL),
('Age', 'age', 'User', 'INT UNSIGNED', 'None', FALSE, NULL),
('Group', 'grp', 'Admin', 'Group', 'None', FALSE, 'Owner'),
('Rate', 'rt', 'GoodAdmin', 'INT', 'None', FALSE, NULL),

('Title', 'title', 'Group', 'VARCHAR(255)', 'None', FALSE, NULL),
('Owner', 'admin', 'Group', 'Admin', 'None', FALSE, 'Group'),
('Users', 'users', 'Group', 'User', 'None', TRUE, NULL);


CALL `nz_admin_db`.alter_db_schema('nz_test_db');