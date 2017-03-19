/*********************************************

			Add types and fields tests

**********************************************/

/*---------------------------------------/
		User and Group types
--------------------------------------*/

CALL before_alter_database_schema();

INSERT INTO `type_add_list`	-- types
(`name`, `table_name`, `base_type_name`)
VALUES
('User', '_user', ''),
('Group', '_group', '');

INSERT INTO `field_add_list`	-- fields
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_nullable`, `is_list`, `compare_options`)
VALUES
-- User
('Login', '_login', 'User', 'VARCHAR(60)', FALSE, FALSE, 'IgnoreCase'),
('Group', '_group', 'User', 'Group', TRUE, FALSE, 'None'),
-- Group
('Title', '_title', 'Group', 'VARCHAR(255)', FALSE, FALSE, 'None'),
('Participants', '_participants', 'Group', 'User', FALSE, TRUE, 'None'),
('DescriptionText', '_description_text', 'Group', 'TEXT', FALSE, FALSE, 'IgnoreCase');

INSERT INTO `backref_upd_list`	-- back references
(`field_owner_type_name`, `field_name`, `new_back_ref_name`)
VALUES
('Group', 'Participants', 'Group');

CALL alter_database_schema('nz_test_db');


/*---------------------------------------/
		Admin and Moderator types
--------------------------------------*/

CALL before_alter_database_schema();

INSERT INTO `type_add_list`	-- types
(`name`, `table_name`, `base_type_name`)
VALUES
('Admin', '_admin', 'User'),
('Moderator', '_moderator', 'User');

INSERT INTO `field_add_list`	-- fields
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_nullable`, `is_list`, `compare_options`)
VALUES
-- Admin
('ControlGroup', '_control_group', 'Admin', 'Group', FALSE, FALSE, 'None'),
-- Moderator
('ModeratedGroup', '_moderated_group', 'Moderator', 'Group', FALSE, FALSE, 'None'),
-- Group
('Admin', '_admin', 'Group', 'Admin', FALSE, FALSE, 'None'),
('Moderators', '_moderators', 'Group', 'Moderator', FALSE, TRUE, 'None');

INSERT INTO `backref_upd_list`	-- back references
(`field_owner_type_name`, `field_name`, `new_back_ref_name`)
VALUES
('Admin', 'ControlGroup', 'Admin'),
('Moderator', 'ModeratedGroup', 'Moderators');

CALL alter_database_schema('nz_test_db');


/*---------------------------------------/
			UberAdmin type
--------------------------------------*/

CALL before_alter_database_schema();

INSERT INTO `type_add_list`	-- types
(`name`, `table_name`, `base_type_name`)
VALUE
('UberAdmin', '_uber_admin', 'Admin');

INSERT INTO `field_add_list`	-- fields
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_nullable`, `is_list`, `compare_options`)
VALUES
('UberRating', '_uber_rating', 'UberAdmin', 'INT', TRUE, FALSE, 'None');

CALL alter_database_schema('nz_test_db');

-- Get nz_test_db schema
USE `nz_test_db`;

SELECT `name`, `base_type_name`
FROM `type`;

SELECT `name`, `owner_type_name`, `type_name`, `is_nullable`, `is_list`, `back_ref_name`, `compare_options`
FROM `field`;
