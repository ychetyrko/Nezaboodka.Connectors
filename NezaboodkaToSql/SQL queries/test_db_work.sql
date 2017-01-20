/*********************************************

	Create Test Nezaboodka database schema
		and get information about it
        
**********************************************/

use `nz_admin_db`;

CALL before_alter_database_list();

insert into `db_rem_list`
values ('nz_test_db');

insert into `db_add_list`
values ('nz_test_db');

CALL alter_database_list();
CALL cleanup_removed_databases();


/*  Fill test database schema */

USE `nz_test_db`;

INSERT INTO `type`
(`name`, `table_name`, `base_type_name`)
VALUES
('User', '_user', ''),
('Admin', '_admin', 'User'),
('Group', '_group', '');


INSERT INTO `field`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
# User
('Login', '_login', 'User', 'VARCHAR(60)', FALSE, 'IgnoreCase', ''),
('Email', '_email', 'User', 'VARCHAR(120)', FALSE, 'IgnoreCase', ''),
('PassHash', '_pass_hash', 'User', 'VARCHAR(128)', FALSE, 'None', ''),
('Group', '_group', 'User', 'Group', FALSE, 'None', 'Participants'),

#Admin
('ControlGroup', '_control_group', 'Admin', 'Group', FALSE, 'None', 'Admin'),

#Group
('Title', '_title', 'Group', 'VARCHAR(255)', FALSE, 'None', ''),
('Admin', '_admin', 'Group', 'Admin', FALSE, 'None', 'ControlGroup'),
('Participants', '_participants', 'Group', 'User', TRUE, 'None', 'Group'),
('Rating', '_rating', 'Group', 'INT UNSIGNED', FALSE, 'None', ''),
('DescriptionText', '_description_text', 'Group', 'TEXT', FALSE, 'IgnoreCase', '');


CALL `nz_admin_db`.alter_db_schema('nz_test_db');

#  *** Get nz_test_db schema ***

USE `nz_test_db`;

SELECT `name`, `base_type_name`
FROM `type`;

SELECT `name`, `owner_type_name`, `type_name`, `is_list`, `back_ref_name`, `compare_options`
FROM `field`;
