/*********************************************

	Create Test Nezaboodka database schema
		and get information about it
        
**********************************************/

USE `nz_admin_db`;

CALL before_alter_database_list();

INSERT INTO `db_rem_list`
VALUES ('nz_test_db');

INSERT INTO `db_add_list`
VALUES ('nz_test_db');

CALL alter_database_list();
CALL cleanup_removed_databases();


/*  Alter test database schema */

CALL before_alter_database_schema();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('User', '_user', ''),
('Admin', '_admin', 'User'),
('Group', '_group', '');

INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_nullable`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
# User
('Login', '_login', 'User', 'VARCHAR(60)', FALSE, FALSE, 'IgnoreCase', ''),
('Email', '_email', 'User', 'VARCHAR(120)', FALSE, FALSE, 'IgnoreCase', ''),
('PassHash', '_pass_hash', 'User', 'VARCHAR(128)',FALSE,  FALSE, 'None', ''),
('Group', '_group', 'User', 'Group', TRUE, FALSE, 'None', 'Participants'),
('Age', '_age', 'User', 'INT UNSIGNED', FALSE, FALSE, 'None', ''),
#Admin
('ControlGroup', '_control_group', 'Admin', 'Group', TRUE, FALSE, 'None', 'Admin'),
-- ('Email', '_email', 'Admin', 'VARCHAR(120)', FALSE, 'IgnoreCase', ''),	-- TODO: HANDLE DUPLICATED FIELDS
#Group
('Title', '_title', 'Group', 'VARCHAR(255)', FALSE, FALSE, 'None', ''),
('Admin', '_admin', 'Group', 'Admin', TRUE, FALSE, 'None', 'ControlGroup'),
('Participants', '_participants', 'Group', 'User', TRUE,  TRUE, 'None', 'Group'),
('Rating', '_rating', 'Group', 'INT UNSIGNED', TRUE, FALSE, 'None', ''),
('DescriptionText', '_description_text', 'Group', 'TEXT', TRUE, FALSE, 'IgnoreCase', '');

CALL alter_database_schema('nz_test_db');

-- Get nz_test_db schema

USE `nz_test_db`;

SELECT `name`, `base_type_name`
FROM `type`;

SELECT `name`, `owner_type_name`, `type_name`, `is_nullable`, `is_list`, `back_ref_name`, `compare_options`
FROM `field`;
