/*======================================
		Add types and fields tests
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
		User and Group types
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('User', '_user', NULL),
('Group', '_group', NULL);
CALL add_types();

/*---------------------------------------/
		User and Group fields
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
-- User
('Login', '_login', 'User', 'VARCHAR(60)', FALSE, 'IgnoreCase', NULL),
('Group', '_group', 'User', 'Group', FALSE, 'None', 'Participants'),
-- Group
('Title', '_title', 'Group', 'VARCHAR(255)', FALSE, 'None', NULL),
('Participants', '_participants', 'Group', 'User', TRUE, 'None', 'Group'),
('DescriptionText', '_description_text', 'Group', 'TEXT', FALSE, 'IgnoreCase', NULL);
CALL add_all_fields();

/*---------------------------------------/
		Admin and Moderator types
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUES
('Admin', '_admin', 'User'),
('Moderator', '_moderator', 'User');
CALL add_types();

/*---------------------------------------/
		Admin and Moderator fields
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
-- Admin
('ControlGroup', '_control_group', 'Admin', 'Group', FALSE, 'None', 'Admin'),
-- Moderator
('ModeratedGroup', '_moderated_group', 'Moderator', 'Group', FALSE, 'None', 'Moderators'),
-- Group
('Admin', '_admin', 'Group', 'Admin', FALSE, 'None', 'ControlGroup'),
('Moderators', '_moderators', 'Group', 'Moderator', TRUE, 'None', 'ModeratedGroup');
CALL add_all_fields();

/*---------------------------------------/
			UberAdmin type
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_add_list`
(`name`, `table_name`, `base_type_name`)
VALUE
('UberAdmin', '_uber_admin', 'Admin');
CALL add_types();

/*---------------------------------------/
			UberAdmin fields
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_add_list`
(`name`, `col_name`, `owner_type_name`, `type_name`, `is_list`, `compare_options`, `back_ref_name`)
VALUES
('UberRating', '_uber_rating', 'UberAdmin', 'INT', FALSE, 'None', NULL);
CALL add_all_fields();
